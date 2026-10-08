package darkgrey.rpg.session.persistence;

/** File-level quarantine shared by Session, Story and their dependent continuations. */
public final class CanonicalSessionDataUnavailableException extends IllegalStateException {

    public static final String CODE = "session_data_unavailable";
    public static final String PLAYER_MESSAGE = "会话／故事存档无法读取，已保留原数据，相关操作已暂停。";

    public CanonicalSessionDataUnavailableException(String name, Throwable cause) {
        super(
            PLAYER_MESSAGE + " SavedData '"
                + name
                + "' requires schema "
                + CanonicalSessionWorldStateNbtCodec.SCHEMA_VERSION
                + ".",
            cause);
    }
}
