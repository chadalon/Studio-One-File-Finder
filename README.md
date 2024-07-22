# Studio One File Finder
Do you also wipe your os and move your samples folder as much as I do, which causes frustration upon opening your old files ("yOu hAvE a MiLLiOn mIsSIng SaMPleS")? Fear not, this will solve all of your problems (except for being single, still figuring that one out)
<br><br>
This is a .NET MAUI application made for Windows and MacOS.

## Using the File Finder

1. **BACKUP YOUR SONG FILES TO A SAFE LOCATION**<br>Whichever songs directory you are going to modify, just copy that entire directory somewhere else. This way you can just copy them back if anything goes wrong. Nothing should happen to your files, but it is important to protect your files just in case.

2. **Select at least one directory which contains your sample files that need to be updated in your S1 songs**<br>These directories you choose do not have to immediately contain your files, the program will search all subfolders until it finds a matching file (or checks the entire contents of every selected folder).
    - Fun fact: you can drag and drop folders into the textboxes.

3. **Select at least one directory which contains your song folders of songs needing to get their sample locations updated**<br>For this one you NEED to have your song folders be the immediate children of the selected folder (this may change in the future)

4. **Update Files**<br>If you have selected at least one valid folder for both your samples and songs, the "Update Files" button will be activated. There are some final settings you can select and you can hover over them for more info. The default selections on these will be good enough for most people (probably). Now click the button, sit back, and watch the magic happen!

5. **Celebrate**<br>You did it! The output window will tell you what's going on, and you'll have a final dialogue popup tell you the overall success. Now go ahead and open your song files and revel in the satisfaction of not having to manually find sample locations for EVERY. SINGLE. SONG. AND. SAMPLEONE/IMPACT PLUGIN. &hearts;

## Important Notes
If this program modifies any Studio One .song files, the date attribute in the Studio One project selection screen will be updated to whenever you ran this program for each file (because...well...it modifies the song files).

**By using this application you accept and assume all risk that comes with it.** This file finder does its best to protect your files, but anything could happen - and nothing would be worse than getting your song files corrupted and lost forever. It is highly recommended to BACKUP YOUR SONGS BEFORE PLAYING WITH THIS APP!

This is **NOT** affiliated, endorsed, or otherwise associated with Studio One or Presonus. I am just a simple guy who got sick of always wanting to check out my older projects only to get my groove thrown off by Studio One (and having no option to update your sample locations in S1's settings. Imagine if there actually is one and this is all for nothing lol).