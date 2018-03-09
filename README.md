# Discontinued

# TawGatherMembersInfo
Automatic gathering of TAW.net members information for automated processing purposes. 
Such as automatic generation of Arma 3 squad xmls.

# Arma squad xMLs:

This project provides regulary and automatically generated squad xmls for all of TAW.net Arma members.
Including all members of AM 1 and AM 2.
Squad xmls are thus always up to date (the changes may take up to several hours to propagate).

### What is squad xml
![What is squad xml](http://am2.taw.net/squadxml/what_is_squadxml.png)

### How to get squad xml
You need to fill in your steam id on your TAW.net profile.
After you fill it, it takes several hours before the system updates it.

Squad url to edit & paste: http://am2.taw.net/squadxml/[your_name].xml

Examples: 
* http://am2.taw.net/squadxml/aeroson.xml
* http://am2.taw.net/squadxml/Ionide.xml
* http://am2.taw.net/squadxml/AckAck.xml

![How to get squad xml](http://am2.taw.net/squadxml/how_to_get_squadxml.png)

### Custom squad xml logo
See [custom-squad-xml-logo](https://github.com/TAW-Arma/TawGatherMembersInfo/tree/master/TawGatherMembersInfo/bin/Release/data/squadxml#custom-squad-xml-logo)

### Custom Arma profile name
See [custom-arma-profile-name](https://github.com/TAW-Arma/TawGatherMembersInfo/tree/master/TawGatherMembersInfo/bin/Release/data/squadxml#custom-arma-profile-name)


# API
This project also provides API to automatically get up to date information of all TAW.net members.
You need to ask AM 2 server staff to give you an authentication token.
The information is however easy to get even without this project for anyone (even not logged in users) at http://taw.net/unit/1/roster.aspx
The resutling list includes all members, even discharged ones, and the order of members always stays the same. New members are added to the bottom.

http://am2.taw.net:8000/?rootUnitId=1&format=table&type=distinct_person_list&fields=name&orderBy=id&version=3&auth=YOUR_AUTH_TOKEN
Results in:

![Example page result](http://image.prntscr.com/image/70e7118657be4a17810f4b19608930e7.png)

It is a html table that can be imported into Google Sheet, for example for administrative purposes.
=IMPORTHTML("http://am2.taw.net:8000/?rootUnitId=1&format=table&type=distinct_person_list&fields=name&orderBy=id&version=3&auth=YOUR_AUTH_TOKEN", "table", 1)

Results in:

![Example google sheet result](http://image.prntscr.com/image/89f57acbb96b41489c61fe08c670fb2a.png)
