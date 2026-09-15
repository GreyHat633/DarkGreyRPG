# Story atlas menu repair

Removed the redundant unlabelled atlas search box; the story list retains search. The atlas context menu now offers only 添加故事 and invokes the existing story creation dialog. Created resources appear at the requested graph position. Generic node creation is blocked for Project scope. Story connections remain manually authored through ports.

Validation: Release WPF suite passed 550/550. On the promoted dist client, invoked the atlas menu, cancelled the existing creation dialog, then reopened it and created GreyHat_:atlas_menu_created in an isolated project. The new node appeared at the right-click position; existing connection count remained one. Dragged exit_b to the second external Flow input; UI and persisted story_logic_graph.json both showed two connections with the correct endpoint IDs.

Evidence: evidence/live/atlas-menu-created-manual-wire.png. Fixture: .tooling/0.3.3.1/atlas-menu-project. Test log: .tooling/0.3.3.1/atlas-menu-tests.log.

Delivery: dist/DarkGreyRPGStudio/DarkGreyRPGStudio.exe; self-contained Windows x64 Release; ProductVersion 0.3.3.1; 141939926 bytes; SHA-256 99A923FFD4402C443B6ED8C134BED9FFC6F0B2B00E48D41FC51DC3793100ADE8.

USER_ACCEPTED=NO. This records implementation and test evidence, not personal user acceptance.
