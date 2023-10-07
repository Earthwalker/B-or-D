# README #

# What is it? #

b-or-d (Temp name) is a place for sharing content through email, somewhat similar to a
[Usenet newsgroup](https://en.wikipedia.org/wiki/Usenet_newsgroup). Everything is done through 
email which is then sorted and re-routed by the software. 

## Posting ##

* User creates message and sends it to the chosen groups for posting
* Server receives the message and checks for malware/spam
* Server forwards the post to the selected groups’ mods if the user is a member of them
* Mods receive the message and forward the post to the server spam account if it does not adhere to
	the groups rules
* Message is forwarded to all members of the group after a certain time

## Signing up ##

* User sends an email to the chosen groups with the subject “join”
* Server adds user to the new members list of the group

## Group users lists ##

* Guests: View content
* Subscribers: Post content
* Mods: Approve posts
* Owners: Add and remove mods

## Groups ##

* Send a message to the desired group name with a certain subject (like “name@mailboard”)
* Owner must already be a poster?

## General commands (not sent to any board) ##

### Subject ###

* Join – receives a list of popular boards and general rules
* Leave – leaves every board
* Rules – receives the general rules
* Boards:Tag1,Tag2 – receives a list of boards with the given tags. All for all tags
* Help – receives help

### To ###

* (*) - if a board with the chosen name exists, the board’s rules are received, otherwise, a new board is created and the user is set as the owner. Users can create up to one board per day.

## Commands for a specific board ##

Each user type has access to lower commands.

### Non-member ###

* Join – joins the board as the default user type
* Rules – receives the board’s rules

### Guests ###

* Leave – leaves the board
* Admin – sends the message to the moderators and owners

### Subscribers ###

* User:SomeUserName – sends the message to the selected user  Each user is now getting his own board to receive messages
* Report:#PostId – reports the message with the given id to the mods A user can just reply to the message to report it to the mods
* (*) - creates a new post

### Moderators ###

* Report:# PostId – reports the message with the given id (decreases the reported user’s points)
* Ban:SomeUserName – changes the given user’s user type to guest and resets his points

### Owners ###

* Rules – updates the rules for the group
* Points:#PointValue – changes the amount of points needed to post without mod verification. -1 for no amount
* Usertype:Usertype – changes the default user type assigned to joining users
* Tags:Tag1,Tag2 – sets the tags for the group
* Leave – leaves the board. Notify other owners. If the only owner, all mods are upgraded to owners
* Owner:SomeUserName - changes the chosen user’s user type to owner
* Mod:SomeUserName – changes the chosen user’s user type to moderator
* Unmod:SomeUserName – changes the chosen user’s user type to subscriber

# Configuration #

b-or-d requires the configuration file "config.ini" to be located in the exe directory.

### Server ###

* Username - user name of the email account (e.g. someusername in someusername@gmail.com)
* Host - the host name of the email account (e.g. gmail.com)
* PopPort - the pop port of the host
* SmtpPort - the SMTP port of the host
* Password - the password of the email account

### Inbox ###
* InboxInterval - frequency in seconds to check for new messages

### Outbox ###
* OutboxInterval - frequency in seconds to send messages

# Contribution guidelines #

* Follow the Style Cop settings included in the project (create an issue if the settings should be modified)
* Create tests for new features
* Run tests after new changes. If some don't pass, either fix the problem or note the problem according to the commit guidelines.
* Follow Git Commit Guidelines established here: https://github.com/angular/angular.js/blob/master/CONTRIBUTING.md#commit

## License
This work is licensed under a [Creative Commons Attribution-NonCommercial 4.0 International License.](https://creativecommons.org/licenses/by-nc/4.0/)