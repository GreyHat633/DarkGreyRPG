package darkgrey.rpg.identity;

import java.nio.charset.StandardCharsets;

/** Canonical validation and filesystem projection for DGR resource IDs. */
public final class DgrResourceId {

    private static final int MAX_NAMESPACE_LENGTH = 32;
    private static final int MAX_LOCAL_LENGTH = 63;
    private static final int MAX_FULL_LENGTH = 96;

    private DgrResourceId() {}

    public static boolean isValidNamespace(String value) {
        if (value == null || value.length() < 1
            || value.length() > MAX_NAMESPACE_LENGTH
            || !isAsciiAlphaNumeric(value.charAt(0))) {
            return false;
        }
        for (int i = 1; i < value.length(); i++) {
            char c = value.charAt(i);
            if (!isAsciiAlphaNumeric(c) && c != '_' && c != '-') {
                return false;
            }
        }
        return true;
    }

    public static boolean isFullId(String value) {
        if (value == null || value.length() > MAX_FULL_LENGTH) {
            return false;
        }
        int separator = value.indexOf(':');
        return separator > 0 && separator == value.lastIndexOf(':')
            && isValidNamespace(value.substring(0, separator))
            && isValidLocal(value.substring(separator + 1));
    }

    public static boolean isCompatibleId(String value) {
        return isFullId(value) || isValidBare(value);
    }

    public static String qualify(String namespace, String local) {
        requireNamespace(namespace);
        requireLocal(local);
        return namespace + ":" + local;
    }

    public static String localId(String id) {
        requireCompatible(id);
        int separator = id.indexOf(':');
        return separator < 0 ? id : id.substring(separator + 1);
    }

    public static String namespace(String id) {
        requireCompatible(id);
        int separator = id.indexOf(':');
        return separator < 0 ? "" : id.substring(0, separator);
    }

    public static String relativeJsonPath(String id) {
        requireCompatible(id);
        int separator = id.indexOf(':');
        if (separator < 0) {
            return id + ".json";
        }
        return "x" + hex(id.substring(0, separator)) + "/x" + hex(id.substring(separator + 1)) + ".json";
    }

    public static String packageFileName(String id) {
        requireCompatible(id);
        int separator = id.indexOf(':');
        if (separator < 0) {
            return id + ".dgrs";
        }
        return "x" + hex(id.substring(0, separator)) + "_x" + hex(id.substring(separator + 1)) + ".dgrs";
    }

    /**
     * Validates the local component of a namespaced DGR ID. Namespaced IDs are
     * case-sensitive; the lowercase-only rule belongs to legacy bare IDs.
     */
    private static boolean isValidLocal(String value) {
        if (value == null || value.length() < 1
            || value.length() > MAX_LOCAL_LENGTH
            || !isAsciiAlphaNumeric(value.charAt(0))) {
            return false;
        }
        for (int i = 1; i < value.length(); i++) {
            char c = value.charAt(i);
            if (!isAsciiAlphaNumeric(c) && c != '_' && c != '.' && c != '-') {
                return false;
            }
        }
        return true;
    }

    private static boolean isValidBare(String value) {
        if (value == null || value.isEmpty() || !isAsciiLowerAlphaNumeric(value.charAt(0))) {
            return false;
        }
        for (int i = 1; i < value.length(); i++) {
            char c = value.charAt(i);
            if (!isAsciiLowerAlphaNumeric(c) && c != '_' && c != '.' && c != '-') {
                return false;
            }
        }
        return true;
    }

    private static boolean isAsciiAlphaNumeric(char c) {
        return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9');
    }

    private static boolean isAsciiLowerAlphaNumeric(char c) {
        return (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9');
    }

    private static void requireNamespace(String value) {
        if (!isValidNamespace(value)) {
            throw new IllegalArgumentException("Invalid DGR namespace.");
        }
    }

    private static void requireLocal(String value) {
        if (!isValidLocal(value)) {
            throw new IllegalArgumentException("Invalid DGR local ID.");
        }
    }

    private static void requireCompatible(String value) {
        if (!isCompatibleId(value)) {
            throw new IllegalArgumentException("Invalid DGR resource ID.");
        }
    }

    private static String hex(String value) {
        byte[] bytes = value.getBytes(StandardCharsets.UTF_8);
        char[] output = new char[bytes.length * 2];
        final char[] digits = "0123456789abcdef".toCharArray();
        for (int i = 0; i < bytes.length; i++) {
            int unsigned = bytes[i] & 0xff;
            output[i * 2] = digits[unsigned >>> 4];
            output[i * 2 + 1] = digits[unsigned & 0x0f];
        }
        return new String(output);
    }
}
