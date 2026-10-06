package darkgrey.rpg.story;

import java.io.File;
import java.util.Collections;
import java.util.Map;

import darkgrey.rpg.dialogue.DialogueDefinition;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ProjectLoadException;
import darkgrey.rpg.quest.QuestDefinition;

/** Rejects retired Story documents; current graphs are loaded by CanonicalProjectContentLoader. */
public final class StoryLoader {

    private StoryLoader() {}

    public static Map<String, StoryDefinition> load(File projectDirectory, Map<String, ActorDefinition> actors,
        Map<String, DialogueDefinition> dialogues, Map<String, QuestDefinition> quests) throws ProjectLoadException {
        File directory = new File(projectDirectory, "stories");
        File[] files = directory.listFiles();
        if (files == null) throw new ProjectLoadException("Cannot list stories directory: " + directory);
        for (File file : files) {
            if (file.isFile() && file.getName()
                .toLowerCase(java.util.Locale.ROOT)
                .endsWith(".json"))
                throw new ProjectLoadException(
                    "Legacy Story format is unsupported; use a current canonical Story: " + file);
        }
        return Collections.emptyMap();
    }

    public static StoryDefinition loadPackagedStory(byte[] bytes, String source, Map<String, ActorDefinition> actors,
        Map<String, DialogueDefinition> dialogues, Map<String, QuestDefinition> quests) throws ProjectLoadException {
        throw new ProjectLoadException("Legacy Story format is unsupported; use a current canonical Story: " + source);
    }

    public static void validatePackagedStories(Map<String, StoryDefinition> stories) throws ProjectLoadException {
        if (stories == null || !stories.isEmpty())
            throw new ProjectLoadException("Legacy Story format is unsupported; use current canonical Stories.");
    }
}
