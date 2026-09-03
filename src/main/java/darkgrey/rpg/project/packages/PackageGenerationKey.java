package darkgrey.rpg.project.packages;

/** Stable logical install identity plus one authoritative content generation. */
public final class PackageGenerationKey {

    private final String packageId;
    private final String storyId;
    private final String contentFingerprint;

    public PackageGenerationKey(String packageId, String storyId, String contentFingerprint) {
        this.packageId = requireId(packageId, "packageId");
        this.storyId = requireId(storyId, "storyId");
        if (contentFingerprint == null || !contentFingerprint.matches("[0-9a-f]{64}"))
            throw new IllegalArgumentException("contentFingerprint must be 64 lowercase hexadecimal characters.");
        this.contentFingerprint = contentFingerprint;
    }

    public static PackageGenerationKey from(LoadedStoryPackage value) {
        if (value == null) throw new IllegalArgumentException("Loaded Story Package is required.");
        return new PackageGenerationKey(value.getPackageId(), value.getStoryId(), value.getContentFingerprint());
    }

    public String getPackageId() {
        return packageId;
    }

    public String getStoryId() {
        return storyId;
    }

    public String getContentFingerprint() {
        return contentFingerprint;
    }

    public String shortFingerprint() {
        return contentFingerprint.substring(0, 12);
    }

    @Override
    public boolean equals(Object other) {
        if (!(other instanceof PackageGenerationKey)) return false;
        PackageGenerationKey that = (PackageGenerationKey) other;
        return packageId.equals(that.packageId) && storyId.equals(that.storyId)
            && contentFingerprint.equals(that.contentFingerprint);
    }

    @Override
    public int hashCode() {
        int result = packageId.hashCode();
        result = 31 * result + storyId.hashCode();
        return 31 * result + contentFingerprint.hashCode();
    }

    private static String requireId(String value, String label) {
        if (value == null || !value.matches("[a-z0-9][a-z0-9_.-]*"))
            throw new IllegalArgumentException(label + " is invalid.");
        return value;
    }
}
