/*
Post-Deployment Script Template							
--------------------------------------------------------------------------------------
 This file contains SQL statements that will be appended to the build script.		
 Use SQLCMD syntax to include a file in the post-deployment script.			
 Example:      :r .\myfile.sql								
 Use SQLCMD syntax to reference a variable in the post-deployment script.		
 Example:      :setvar TableName MyTable							
               SELECT * FROM [$(TableName)]					
--------------------------------------------------------------------------------------
*/

use pvhistory;

delete from pvhistory.version;
insert into pvhistory.version (Major, Minor, Release, Patch)
values ('1', '4', '1', '2');

/* drop user [NT AUTHORITY\LOCAL SERVICE]; */
create user [NT AUTHORITY\LOCAL SERVICE] without login with default_schema = pvhistory;
grant CONTROL to [NT AUTHORITY\LOCAL SERVICE];



