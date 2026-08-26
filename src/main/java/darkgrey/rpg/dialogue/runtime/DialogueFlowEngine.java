package darkgrey.rpg.dialogue.runtime;

import darkgrey.rpg.dialogue.ChoiceNode;
import darkgrey.rpg.dialogue.DialogueDefinition;
import darkgrey.rpg.dialogue.DialogueNode;
import darkgrey.rpg.dialogue.EndNode;
import darkgrey.rpg.dialogue.JumpNode;
import darkgrey.rpg.dialogue.LineNode;

public final class DialogueFlowEngine {

    public static final int CONTINUE_ACTION = -1;
    private static final int MAX_AUTOMATIC_STEPS = 128;

    private DialogueFlowEngine() {}

    public static ResolvedStep resolve(DialogueDefinition dialogue, String nodeId) {
        String current = nodeId;
        for (int step = 0; step < MAX_AUTOMATIC_STEPS; step++) {
            DialogueNode node = dialogue.getNode(current);
            if (node == null) {
                return ResolvedStep.error("Dialogue reached a missing node: " + current);
            }
            if (node instanceof JumpNode) {
                current = ((JumpNode) node).getTarget();
                continue;
            }
            if (node instanceof EndNode) {
                return ResolvedStep.result(((EndNode) node).getResult());
            }
            return ResolvedStep.interactive(node);
        }
        return ResolvedStep.error("Dialogue exceeded the automatic Jump limit.");
    }

    public static String advance(DialogueNode node, int choiceIndex) {
        if (node instanceof LineNode && choiceIndex == CONTINUE_ACTION) {
            return ((LineNode) node).getNext();
        }
        if (node instanceof ChoiceNode) {
            ChoiceNode choice = (ChoiceNode) node;
            if (choiceIndex >= 0 && choiceIndex < choice.getChoices()
                .size()) {
                return choice.getChoices()
                    .get(choiceIndex)
                    .getNext();
            }
        }
        return null;
    }

    public static final class ResolvedStep {

        private final DialogueNode node;
        private final String result;
        private final String error;

        private ResolvedStep(DialogueNode node, String result, String error) {
            this.node = node;
            this.result = result;
            this.error = error;
        }

        public static ResolvedStep interactive(DialogueNode node) {
            return new ResolvedStep(node, null, null);
        }

        public static ResolvedStep result(String result) {
            return new ResolvedStep(null, result, null);
        }

        public static ResolvedStep error(String error) {
            return new ResolvedStep(null, null, error);
        }

        public DialogueNode getNode() {
            return node;
        }

        public String getResult() {
            return result;
        }

        public String getError() {
            return error;
        }
    }
}
