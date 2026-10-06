package darkgrey.rpg.project.packages;

import java.io.ByteArrayOutputStream;
import java.io.DataOutputStream;
import java.io.IOException;
import java.io.StringReader;
import java.nio.ByteBuffer;
import java.nio.CharBuffer;
import java.nio.charset.CodingErrorAction;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.ArrayList;
import java.util.Comparator;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.TreeMap;

import com.google.gson.stream.JsonReader;
import com.google.gson.stream.JsonToken;

import darkgrey.rpg.identity.StoryUid;

/** SHA-256 of the same canonical token stream used by Studio. No outer container hash. */
public final class StoryMemberFingerprint {

    public static final int MAX_RECORDS = 4096;
    public static final int MAX_BYTES = 64 * 1024 * 1024;

    public static final class Content {

        private final String role;
        private final String identity;
        private final String runtimeJson;

        public Content(String role, String identity, String runtimeJson) {
            if (role == null || role.isEmpty() || identity == null || identity.isEmpty() || runtimeJson == null)
                throw new IllegalArgumentException("Fingerprint records require role, identity and content.");
            this.role = role;
            this.identity = identity;
            this.runtimeJson = runtimeJson;
        }
    }

    private StoryMemberFingerprint() {}

    public static String compute(StoryUid uid, Iterable<Content> content) throws IOException {
        if (uid == null || content == null)
            throw new IllegalArgumentException("Story fingerprint inputs are required.");
        List<Content> records = new ArrayList<Content>();
        Set<List<String>> identities = new HashSet<List<String>>();
        for (Content record : content) {
            if (record == null || !identities.add(java.util.Arrays.asList(record.role, record.identity)))
                throw new IOException("Duplicate or null Story fingerprint record.");
            records.add(record);
            if (records.size() > MAX_RECORDS) throw new IOException("Story fingerprint record budget exceeded.");
        }
        records.sort(
            Comparator.comparing((Content value) -> value.role)
                .thenComparing(value -> value.identity));
        BudgetBuffer bytes = new BudgetBuffer();
        DataOutputStream output = new DataOutputStream(bytes);
        output.write("DGR-STORY-CONTENT-V1\0".getBytes(StandardCharsets.US_ASCII));
        text(output, uid.getValue());
        output.writeInt(records.size());
        for (Content record : records) {
            text(output, record.role);
            text(output, record.identity);
            // Gson 2.2.4 rejects top-level primitives in strict mode. An array
            // wrapper permits exactly one JSON value without enabling leniency.
            try (JsonReader reader = new JsonReader(new StringReader("[" + record.runtimeJson + "]"))) {
                reader.setLenient(false);
                reader.beginArray();
                value(reader, output, 0);
                if (reader.hasNext()) throw new IOException("Trailing fingerprint JSON content.");
                reader.endArray();
                if (reader.peek() != JsonToken.END_DOCUMENT)
                    throw new IOException("Trailing fingerprint JSON content.");
            }
        }
        try {
            byte[] hash = MessageDigest.getInstance("SHA-256")
                .digest(bytes.toByteArray());
            StringBuilder result = new StringBuilder();
            for (byte part : hash) result.append(String.format(java.util.Locale.ROOT, "%02x", part & 255));
            return result.toString();
        } catch (NoSuchAlgorithmException exception) {
            throw new IllegalStateException(exception);
        }
    }

    private static void text(DataOutputStream output, String value) throws IOException {
        ByteBuffer bytes = StandardCharsets.UTF_8.newEncoder()
            .onMalformedInput(CodingErrorAction.REPORT)
            .onUnmappableCharacter(CodingErrorAction.REPORT)
            .encode(CharBuffer.wrap(value));
        output.writeInt(bytes.remaining());
        output.write(bytes.array(), bytes.position(), bytes.remaining());
    }

    private static void value(JsonReader reader, DataOutputStream output, int depth) throws IOException {
        if (depth > 64) throw new IOException("Story fingerprint nesting budget exceeded.");
        switch (reader.peek()) {
            case NULL:
                reader.nextNull();
                output.writeByte('n');
                return;
            case BOOLEAN:
                output.writeByte(reader.nextBoolean() ? 't' : 'f');
                return;
            case STRING:
                output.writeByte('s');
                text(output, reader.nextString());
                return;
            case NUMBER:
                double number = reader.nextDouble();
                if (Double.isInfinite(number) || Double.isNaN(number) || Math.abs(number) > 9007199254740991d)
                    throw new IOException("Fingerprint numbers must be finite and within the exact integer range.");
                output.writeByte('d');
                output.writeLong(Double.doubleToLongBits(number == 0 ? 0 : number));
                return;
            case BEGIN_ARRAY:
                reader.beginArray();
                BudgetBuffer elements = new BudgetBuffer();
                DataOutputStream array = new DataOutputStream(elements);
                int count = 0;
                while (reader.hasNext()) {
                    value(reader, array, depth + 1);
                    count++;
                }
                reader.endArray();
                output.writeByte('a');
                output.writeInt(count);
                elements.writeTo(output);
                return;
            case BEGIN_OBJECT:
                reader.beginObject();
                Map<String, byte[]> properties = new TreeMap<String, byte[]>();
                long total = 0;
                while (reader.hasNext()) {
                    String key = reader.nextName();
                    if (properties.containsKey(key))
                        throw new IOException("Duplicate JSON field in Story fingerprint content.");
                    BudgetBuffer property = new BudgetBuffer();
                    value(reader, new DataOutputStream(property), depth + 1);
                    total += 4L + key.length() * 3L + property.size();
                    if (total > MAX_BYTES) throw new IOException("Story fingerprint byte budget exceeded.");
                    properties.put(key, property.toByteArray());
                }
                reader.endObject();
                output.writeByte('o');
                output.writeInt(properties.size());
                for (Map.Entry<String, byte[]> property : properties.entrySet()) {
                    text(output, property.getKey());
                    output.write(property.getValue());
                }
                return;
            default:
                throw new IOException("Invalid Story fingerprint JSON content.");
        }
    }

    private static final class BudgetBuffer extends ByteArrayOutputStream {

        @Override
        public synchronized void write(int value) {
            require(1);
            super.write(value);
        }

        @Override
        public synchronized void write(byte[] value, int offset, int length) {
            require(length);
            super.write(value, offset, length);
        }

        private void require(int length) {
            if (length > MAX_BYTES - count)
                throw new IllegalArgumentException("Story fingerprint byte budget exceeded.");
        }
    }
}
