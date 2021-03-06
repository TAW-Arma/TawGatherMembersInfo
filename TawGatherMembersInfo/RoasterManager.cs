﻿using Neitri;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TawGatherMembersInfo
{
	public class RoasterManager : IOnDependenciesResolved
	{
		public event Action OnDataGatheringCycleCompleted;

		Task mainTask;
		CancellationTokenSource cancel = new CancellationTokenSource();

		[Dependency]
		DbContextProvider db;

		[Dependency]
		FileSystem fileSystem;

		[Dependency]
		Config config;

		[Dependency]
		IDependencyManager dependency;

		ILogEnd Log => Program.Log;

		SessionMannager sessionManager;

		WebDataParser dataParser;

		public void OnDependenciesResolved()
		{
			sessionManager = new SessionMannager(config.MaxConcurrentWebSessions, config.MaxWebRequestsPerMinutePerSession);
			dependency.Register(sessionManager);

			dataParser = dependency.Create<WebDataParser>();
		}

		public void Join()
		{
			mainTask?.Wait();
		}

		public void Run()
		{
			mainTask = Task.Run(ThreadMain, cancel.Token);
		}

		public void Stop()
		{
			cancel?.Cancel();
		}

		/// <summary>
		/// Gather basic information from unit roaster, still needs more detailed updating for each person from his/her profile page.
		/// </summary>
		void GatherBasicInformationFromUnitId1Roaster()
		{
			Task.Run(async () =>
			{
				await dataParser.UpdateUnitContents(Log, sessionManager, 1);
			}).Wait();
		}

		void BackupPeopleOrder()
		{
			using (var log = Log.ProfileStart("backing up people names order"))
			{
				var file = fileSystem.GetFile("backup.personsOrder.txt");
				using (var data = db.NewContext)
				{
					var people = data.People.OrderBy(p => p.PersonId).Select(p => p.Name).ToArray();
					File.WriteAllLines(file, people);
				}
			}
		}

		void UpdateActivePeopleProfiles()
		{
			var log = Log.ProfileStart("update active people profiles");
			Task.Run(async () =>
			{
				await dataParser.UpdateActivePeopleProfiles(log);
			}).Wait();
			log.End();
		}

		void UpdateOldEvents()
		{
			var maxDaysBack = config.GetOne(45, "ReparseExistingEventsThatAreDaysBack");
			maxDaysBack = 300;

			var log = Log.ScopeStart($"updating old events {maxDaysBack} days back");

			long[] eventTawIdsToUpdate;
			using (var data = db.NewContext)
			{
				var afterDate = DateTime.UtcNow.AddDays(-maxDaysBack);
				eventTawIdsToUpdate = data.Events.Where(e => e.From > afterDate).Select(e => e.TawId).ToArray();
			}

			var tasks = new List<Task>();
			foreach (var eventTawId in eventTawIdsToUpdate)
			{
				var task = Task.Run(async () =>
				{
					var result = await dataParser.ParseEventData(log, eventTawId);
				});
				tasks.Add(task);
			}

			Task.WaitAll(tasks.ToArray());

			log.End();
		}

		bool ShouldSkipEvent(long tawEventId)
		{
			if (tawEventId < 114) return true; // first valid event is 114
			if (tawEventId >= 32628 && tawEventId <= 33626) return true; // missing events, hard to tell if its last event or just missing one
			if (tawEventId >= 38804 && tawEventId <= 39801) return true; // again missing fucking event
			return false;
		}

		void GatherNewEvents()
		{
			var log = Log.ScopeStart($"gathering new events");

			long eventIdStart;
			using (var data = db.NewContext) eventIdStart = data.Events.OrderByDescending(e => e.TawId).Take(1).Select(e => e.TawId).FirstOrDefault();
			// if (eventIdStart == default(long)) eventIdStart = 0; // 65000 is theoretically enough, it is about 1 year back, but sometimes we want more

			var doBreak = new System.Threading.ManualResetEventSlim();

			var tasks = new List<Task>();

			var pauseEachXEvents = 1;

			for (long i = 1; i < 100000; i++)
			{
				long eventId = eventIdStart + i;

				if (ShouldSkipEvent(eventId)) continue;
				// TODO:
				// a clever algorithm that works on ranges, e.g: try eventId+2  eventId+4 .. eventId+1024,
				// then eventId+1024-2 eventId+1024-4 eventId+1024-128
				// find the next event that works by checking suitable ranges

				if (doBreak.IsSet) break;
				var task = Task.Run(async () =>
				{
					if (doBreak.IsSet) return;
					var result = await dataParser.ParseEventData(log, eventId);
					if (result == WebDataParser.ParseEventResult.InvalidUriShouldRetry)
					{
						await Task.Delay(500);
						if (doBreak.IsSet) return;
						result = await dataParser.ParseEventData(log, eventId);
						if (result == WebDataParser.ParseEventResult.InvalidUriShouldRetry)
						{
							await Task.Delay(500);
							if (doBreak.IsSet) return;
							result = await dataParser.ParseEventData(log, eventId);
							if (result == WebDataParser.ParseEventResult.InvalidUriShouldRetry)
							{
								log.Warn("retried to parse event taw id:" + eventId + " already 3 times, failed all of them, probably last event, stopping event parsing");
								doBreak.Set();
							}
						}
					}
					else
					{
						if (pauseEachXEvents <= 100) Interlocked.Increment(ref pauseEachXEvents); // successfull, lets parse more at the same time
					}
				});

				tasks.Add(task);

				if (i % pauseEachXEvents == 0)
				{
					tasks.WaitAll(Log);
					tasks.Clear();
				}
			}

			tasks.WaitAll(Log);

			log.End();
		}

		async Task ReparseMissingEvents()
		{
			var log = Log.ProfileStart("reparsing missing events");

			var missingTawIds = new List<long>(2048);

			long[] tawIds;
			using (var data = db.NewContext) tawIds = data.Events.OrderBy(e => e.TawId).Select(e => e.TawId).ToArray();

			long expectedTawId = 0;
			foreach (var tawId in tawIds)
			{
				while (tawId != expectedTawId)
				{
					if (!ShouldSkipEvent(expectedTawId)) missingTawIds.Add(expectedTawId);
					expectedTawId++;
				}
				expectedTawId = tawId + 1; // we expect this id next
			}

			log.Info("taw event id gaps:" + missingTawIds.Count);

			await Task.WhenAll(
				missingTawIds.Select(tawId => Task.Run(async () => await dataParser.ParseEventData(log, tawId)))
			);

			log.End();
		}

		void Run(Action action)
		{
			try
			{
				action();
			}
			catch (Exception e)
			{
				Log.FatalException(e);
			}
		}

		async Task Delay()
		{
			var secondsLeft = config.WebCrawlerLoopPauseSeconds;
			var log = Log.ScopeStart($"pausing data gathering loop");

			var logEverySeconds = (int)(secondsLeft / 100);
			if (logEverySeconds < 1) logEverySeconds = 1;

			while (secondsLeft > 0)
			{
				log.Trace($"{secondsLeft} seconds to go");
				if (secondsLeft > logEverySeconds) await Task.Delay(logEverySeconds * 1000);
				else await Task.Delay(secondsLeft * 1000);
				secondsLeft -= logEverySeconds;
			}

			log.End();
		}

		bool Chance(int percent)
		{
			var r = new Random();
			return percent >= r.Next(101);
		}

		async Task ThreadMain()
		{
			long i = 0;

			//DEBUG
			// await dataParser.UpdateInfoFromProfilePage(Log, "Colt");
			Run(() => UpdateActivePeopleProfiles());

			while (true)
			{
				i++;
				Log.Info(nameof(ThreadMain) + " loop number #" + i);

				Run(() => GatherBasicInformationFromUnitId1Roaster());
				Run(() => BackupPeopleOrder());

				if (Chance(config.GetOne(30, "ChancePerLoopToUpdateActivePeopleProfiles")))
					Run(() => UpdateActivePeopleProfiles());

				Run(() => OnDataGatheringCycleCompleted?.Invoke());

				if (Chance(config.GetOne(10, "ChancePerLoopToReparseExistingEvents")))
					Run(() => UpdateOldEvents());

				if (Chance(config.GetOne(50, "ChancePerLoopToParseNewEvents")))
					Run(() => GatherNewEvents());

				if (Chance(config.GetOne(1, "ChancePerLoopToReparseMissingEvents")))
					Run(async () => await ReparseMissingEvents());

				await Delay();
			}
		}
	}
}