package darkgrey.rpg.project.packages;

import java.nio.charset.StandardCharsets;

import darkgrey.rpg.project.ProjectDefinition;
import darkgrey.rpg.project.ProjectLoadException;
import darkgrey.rpg.project.ProjectRepository;

/** Current project provenance validation; resource ownership uses Story UID. */
public final class OfflineOriginProbe {

    private OfflineOriginProbe() {}

    public static void main(String[] args) throws Exception {
        assertProjectOriginParsing();
        System.out.println("CURRENT_PROJECT_ORIGIN_PARSE=PASS");
    }

    private static void assertProjectOriginParsing() throws Exception {
        for (String id : new String[] { "TestProject2", "testproject2" }) {
            ProjectDefinition project = ProjectRepository.readPackagedProject(
                json(
                    "{\"schema_version\":3,\"identity_format\":\"story-uid-v1\",\"id\":\"" + id
                        + "\",\"display_name\":\"Probe\"}"),
                "case-project.json");
            require(id.equals(project.getId()), "Project ID casing was changed");
        }
        System.out.println("PROJECT_ID_CASE_PRESERVED=PASS");
        ProjectDefinition explicit = ProjectRepository.readPackagedProject(
            json(
                "{\"schema_version\":3,\"identity_format\":\"story-uid-v1\",\"id\":\"legacy-id\",\"display_name\":\"Probe\","
                    + "\"project_origin_code\":\" origin-a \"}"),
            "explicit-project.json");
        require(" origin-a ".equals(explicit.getProjectOriginCode()), "Explicit project origin was not preserved");

        ProjectDefinition legacy = ProjectRepository.readPackagedProject(
            json(
                "{\"schema_version\":3,\"identity_format\":\"story-uid-v1\",\"id\":\"legacy-id\",\"display_name\":\"Probe\"}"),
            "legacy-project.json");
        require("legacy-id".equals(legacy.getProjectOriginCode()), "Legacy project origin did not fall back to id");

        expectProjectLoad(
            "{\"schema_version\":3,\"identity_format\":\"story-uid-v1\",\"id\":\"legacy-id\",\"display_name\":\"Probe\","
                + "\"project_origin_code\":\"   \"}",
            "Blank project_origin_code was accepted");
        expectProjectLoad(
            "{\"schema_version\":3,\"identity_format\":\"story-uid-v1\",\"id\":\"legacy-id\",\"display_name\":\"Probe\","
                + "\"project_origin_code\":17}",
            "Non-string project_origin_code was accepted");
        expectProjectLoad(
            "{\"schema_version\":3,\"identity_format\":\"story-uid-v1\",\"id\":\"legacy-id\",\"display_name\":\"Probe\","
                + "\"project_origin_code\":null}",
            "Null project_origin_code was accepted");
    }

    private static byte[] json(String value) {
        return value.getBytes(StandardCharsets.UTF_8);
    }

    private static void expectProjectLoad(String source, String message) throws Exception {
        try {
            ProjectRepository.readPackagedProject(json(source), "invalid-project.json");
            throw new AssertionError(message);
        } catch (ProjectLoadException expected) {}
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
