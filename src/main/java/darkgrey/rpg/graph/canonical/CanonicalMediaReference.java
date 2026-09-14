package darkgrey.rpg.graph.canonical;

/** Internal content-addressed runtime media path; never an author-created ID. */
public final class CanonicalMediaReference {

    private CanonicalMediaReference() {}

    public static boolean isValid(String value) {
        return value != null && value.matches("media/[0-9a-f]{64}\\.(png|jpg|ogg)");
    }

    public static boolean isImage(String value) {
        return isValid(value) && (value.endsWith(".png") || value.endsWith(".jpg"));
    }

    public static boolean isAudio(String value) {
        return isValid(value) && value.endsWith(".ogg");
    }
}
