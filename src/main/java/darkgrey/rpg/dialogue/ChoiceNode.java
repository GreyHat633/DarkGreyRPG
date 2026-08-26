package darkgrey.rpg.dialogue;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

public final class ChoiceNode extends DialogueNode {

    private final String prompt;
    private final List<ChoiceOption> choices;

    public ChoiceNode(String id, String prompt, List<ChoiceOption> choices) {
        super(id, DialogueNodeType.CHOICE);
        this.prompt = prompt;
        this.choices = Collections.unmodifiableList(new ArrayList<ChoiceOption>(choices));
    }

    public String getPrompt() {
        return prompt;
    }

    public List<ChoiceOption> getChoices() {
        return choices;
    }
}
