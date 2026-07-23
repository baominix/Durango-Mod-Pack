# Task System Restoration Plugin

Restores the client `Quest`/Task page for offline play.

The client data contains 1,386 task definitions across 38 categories. Important groups include:

- `permanent`: 541
- `sunset`: 101 (Epic/story, kept separate from Task tabs)
- `daily`: 30
- `weekly`: 64
- seasonal/event and returner categories.

The stock offline server only advertises `sunset` as Epic and then returns the same Epic list for every request. The retail `QuestSystem` intentionally excludes Epic from the visible Task tabs, leaving the page empty.

Version 0.1.0 restores visible Permanent, Daily and Weekly tabs, serves each requested category from the real client `QuestYml` catalog, initializes score information and answers quest-state queries. It is a catalog/research restoration stage: it does not yet emulate the live server's objective progress, rotation, expiration or reward grants.

`MaxTasksPerCategory` defaults to 120 to avoid overloading the old UI; it can be changed in the BepInEx config.
