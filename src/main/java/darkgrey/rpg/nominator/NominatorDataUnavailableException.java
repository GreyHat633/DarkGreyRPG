package darkgrey.rpg.nominator;

/** A quarantined registry must never become empty writable data or escape an operation boundary. */
public final class NominatorDataUnavailableException extends IllegalStateException {

    public static final String CODE = "nominator_data_unavailable";
    public static final String PLAYER_MESSAGE = "实体指名数据无法读取或格式不兼容，已保留原数据，本次操作已取消。";

    public NominatorDataUnavailableException(String name, Throwable cause) {
        super(
            "Nominator SavedData '" + name
                + "' is unavailable; current schema "
                + NominatorSavedData.SCHEMA_VERSION
                + " is required and original data is protected.",
            cause);
    }
}
