package darkgrey.rpg.project.packages;

import java.io.File;
import java.io.FileOutputStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.List;
import java.util.zip.ZipEntry;
import java.util.zip.ZipOutputStream;

import darkgrey.rpg.project.ProjectLoadException;

/** Focused contract probe for the metadata-only DGRS archive reader. */
public final class DgrsArchiveReaderProbe {

    private DgrsArchiveReaderProbe() {}

    public static void main(String[] args) throws Exception {
        if (args.length != 1) throw new IllegalArgumentException("Expected <E-drive probe root>");
        File root = new File(args[0]).getAbsoluteFile();
        if (!root.exists() && !root.mkdirs()) throw new IllegalStateException("Cannot create probe root: " + root);
        File archive = new File(root, "valid.dgrs");
        try {
            writeArchive(
                archive,
                new Entry("manifest.json", "{\"format\":\"dgrs\"}"),
                new Entry("project.json", "{\"name\":\"演示\"}"),
                new Entry("content/unicode.txt", "中文内容"));
            DgrsArchiveReader reader = DgrsArchiveReader.open(archive);
            require(reader.contains("manifest.json"), "manifest was not indexed");
            require(reader.contains("project.json"), "project was not indexed");
            require("中文内容".equals(reader.readUtf8("content/unicode.txt")), "UTF-8 content changed");
            byte[] bytes = reader.readBytes("content/unicode.txt");
            bytes[0] = (byte) 'X';
            require("中文内容".equals(reader.readUtf8("content/unicode.txt")), "entry bytes are not detached");
            List<String> names = reader.getEntryNames();
            try {
                names.clear();
                throw new AssertionError("entry-name collection is mutable");
            } catch (UnsupportedOperationException expected) {
                // expected
            }
            require(!new File(root, ".dgrs-runtime").exists(), "reader created a runtime directory");
            require(
                "{\"name\":\"演示\"}".equals(reader.readUtf8("project.json")),
                "metadata reader could not reopen the source archive");
            System.out.println("DGRS_ARCHIVE_READER_VALID=PASS");
            System.out.println("DGRS_ARCHIVE_READER_METADATA_ONLY=PASS");

            Entry[] tooManyEntries = new Entry[DgrsArchiveReader.MAX_ENTRY_COUNT + 1];
            tooManyEntries[0] = new Entry("manifest.json", "{}");
            tooManyEntries[1] = new Entry("project.json", "{}");
            for (int i = 2; i < tooManyEntries.length; i++)
                tooManyEntries[i] = new Entry("content/entry-" + i + ".txt", "x");
            expectReject(writeArchive(new File(root, "too-many-entries.dgrs"), tooManyEntries), "entry count budget");
            System.out.println("DGRS_ARCHIVE_READER_ENTRY_COUNT_BUDGET=PASS");

            byte[] maxEntry = new byte[(int) DgrsArchiveReader.MAX_ENTRY_BYTES];
            expectReject(
                writeArchive(
                    new File(root, "too-many-bytes.dgrs"),
                    new Entry("manifest.json", "{}"),
                    new Entry("project.json", "{}"),
                    new Entry("content/large-1.bin", maxEntry),
                    new Entry("content/large-2.bin", maxEntry),
                    new Entry("content/large-3.bin", maxEntry),
                    new Entry("content/large-4.bin", maxEntry)),
                "aggregate uncompressed byte budget");
            System.out.println("DGRS_ARCHIVE_READER_TOTAL_BYTE_BUDGET=PASS");

            String[] unsafe = { "", "../escape.txt", "a/../escape.txt", "a//b.txt", "a/./b.txt", "a\\b.txt",
                "/absolute.txt", "C:/drive.txt", "folder:name.txt", "folder/" };
            for (int i = 0; i < unsafe.length; i++) {
                File candidate = new File(root, "unsafe-" + i + ".dgrs");
                writeArchive(
                    candidate,
                    new Entry("manifest.json", "{}"),
                    new Entry("project.json", "{}"),
                    new Entry(unsafe[i], "bad"));
                expectReject(candidate, "unsafe path " + unsafe[i]);
            }
            expectReject(
                writeArchive(
                    new File(root, "duplicate.dgrs"),
                    new Entry("manifest.json", "{}"),
                    new Entry("project.json", "{}"),
                    new Entry("Actors/a.json", "1"),
                    new Entry("actors/A.json", "2")),
                "case-insensitive duplicate");
            expectReject(
                writeArchive(new File(root, "missing-manifest.dgrs"), new Entry("project.json", "{}")),
                "missing manifest");
            expectReject(
                writeArchive(new File(root, "missing-project.dgrs"), new Entry("manifest.json", "{}")),
                "missing project");
            Files.write(new File(root, "corrupt.dgrs").toPath(), new byte[] { 0x44, 0x47, 0x52, 0x53 });
            expectReject(new File(root, "corrupt.dgrs"), "corrupt ZIP");
            File invalidUtf8 = writeArchive(
                new File(root, "invalid-utf8.dgrs"),
                new Entry("manifest.json", "{}"),
                new Entry("project.json", "{}"),
                new Entry("invalid.json", new byte[] { (byte) 0xC3, (byte) 0x28 }));
            try {
                DgrsArchiveReader.open(invalidUtf8)
                    .readUtf8("invalid.json");
                throw new AssertionError("Accepted invalid UTF-8");
            } catch (ProjectLoadException expected) {
                // expected
            }
            String manifestJson = manifest();
            StoryPackageManifest parsed = StoryPackageManifest
                .read(manifestJson.getBytes(StandardCharsets.UTF_8), "valid.dgrs!/manifest.json");
            require(parsed.isDgrsV1(), "valid DGRS manifest identity was not retained");
            expectManifestReject(manifestJson.replace("\"format_version\":1", "\"format_version\":999"));
            expectManifestReject(
                manifestJson.replace("\"actors\":[]", "\"actors\":[\"Actors/a.json\",\"actors/A.json\"]"));
            expectManifestReject(manifestJson.replace("\"actors\":[]", "\"actors\":[\"actors:bad/a.json\"]"));
            File missingDeclared = writeArchive(
                new File(root, "missing-declared.dgrs"),
                new Entry("manifest.json", manifestJson),
                new Entry("project.json", "{\"schema_version\":2,\"id\":\"test\",\"display_name\":\"Test\"}"));
            try {
                DgrsArchiveReader missingReader = DgrsArchiveReader.open(missingDeclared);
                StoryPackageSnapshotReader.read(
                    missingReader,
                    StoryPackageManifest.read(
                        missingReader.readBytes("manifest.json"),
                        missingReader.getSourceIdentity() + "!/manifest.json"));
                throw new AssertionError("Accepted a missing manifest-declared resource");
            } catch (ProjectLoadException expected) {
                // expected
            }
            System.out.println("DGRS_ARCHIVE_READER_UNSAFE_REJECTED=PASS");
            System.out.println("DGRS_ARCHIVE_READER_REQUIRED_ENTRIES=PASS");
            System.out.println("DGRS_ARCHIVE_READER_CORRUPT_REJECTED=PASS");
            System.out.println("DGRS_ARCHIVE_READER_UTF8_REJECTED=PASS");
            System.out.println("DGRS_MANIFEST_PARITY=PASS");
            System.out.println("DGRS_DECLARED_ENTRY_REQUIRED=PASS");
        } finally {
            delete(root);
        }
    }

    private static File writeArchive(File file, Entry... entries) throws Exception {
        ZipOutputStream output = new ZipOutputStream(new FileOutputStream(file));
        try {
            for (Entry value : entries) {
                output.putNextEntry(new ZipEntry(value.name));
                if (value.name.length() > 0) output.write(value.content);
                output.closeEntry();
            }
        } finally {
            output.close();
        }
        return file;
    }

    private static void expectReject(File file, String label) throws Exception {
        try {
            DgrsArchiveReader.open(file);
            throw new AssertionError("Accepted " + label);
        } catch (ProjectLoadException expected) {
            // expected
        }
    }

    private static void expectManifestReject(String json) throws Exception {
        try {
            StoryPackageManifest.read(json.getBytes(StandardCharsets.UTF_8), "invalid.dgrs!/manifest.json");
            throw new AssertionError("Accepted an invalid DGRS manifest");
        } catch (ProjectLoadException expected) {
            // expected
        }
    }

    private static String manifest() {
        return "{\"format\":\"dgrs\",\"format_version\":1,\"producer\":\"DarkGreyRPGStudio\","
            + "\"producer_version\":\"0.3.2.0\",\"schema_version\":1,\"package_id\":\"story\","
            + "\"package_version\":\"0.3.2.0\",\"story_id\":\"story\",\"story_schema_version\":1,"
            + "\"required_resources\":{\"story\":\"resources/canonical/stories/story.json\","
            + "\"actors\":[],\"items\":[],\"item_groups\":[],\"dialogues\":[],\"quests\":[],"
            + "\"canonical_stories\":[\"resources/canonical/stories/story.json\"],"
            + "\"canonical_memberships\":[],\"sessions\":[],\"tasks\":[]}}";
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }

    private static void delete(File file) {
        if (file.isDirectory()) {
            File[] children = file.listFiles();
            if (children != null) for (File child : children) delete(child);
        }
        if (!file.delete() && file.exists()) throw new AssertionError("Cannot clean probe path: " + file);
    }

    private static final class Entry {

        private final String name;
        private final byte[] content;

        private Entry(String name, String content) {
            this(name, content.getBytes(StandardCharsets.UTF_8));
        }

        private Entry(String name, byte[] content) {
            this.name = name;
            this.content = content;
        }
    }
}
