package darkgrey.rpg.task.persistence;

/** Fixed lowercase ASCII SHA-256 grammar, shared by journal and player-image validation. */
public final class TaskReceiptKey {

    private TaskReceiptKey() {}

    public static boolean isValid(String value) {
        if (value == null || value.length() != 64) return false;
        for (int i = 0; i < value.length(); i++) {
            char character = value.charAt(i);
            if (!(character >= '0' && character <= '9' || character >= 'a' && character <= 'f')) return false;
        }
        return true;
    }
}
