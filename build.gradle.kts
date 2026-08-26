plugins {
    id("com.gtnewhorizons.gtnhconvention")
}

val releaseVersion = providers.gradleProperty("modVersion").get()
version = releaseVersion

tasks.withType<org.gradle.jvm.tasks.Jar>().configureEach {
    archiveVersion.set(releaseVersion)
}

tasks.withType<Test>().configureEach {
    failOnNoDiscoveredTests.set(false)
}

tasks.register<JavaExec>("phase1ProjectProbe") {
    group = "verification"
    description = "Runs the Phase 1 project loader and snapshot rollback probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.project.ProjectRepositoryProbe")
    args(
        layout.buildDirectory.dir("phase1-runtime-probe").get().asFile.absolutePath,
        layout.projectDirectory.dir("examples/phase4_project").asFile.absolutePath,
    )
}

tasks.register("phase2DialogueProbe") {
    group = "verification"
    description = "Runs Dialogue loading, flow, Result, and network codec probes."
    dependsOn("phase1ProjectProbe")
}

tasks.register("phase3QuestProbe") {
    group = "verification"
    description = "Runs Quest loading, groups, progress, persistence, and Journal codec probes."
    dependsOn("phase1ProjectProbe")
}

tasks.register("phase4StoryProbe") {
    group = "verification"
    description = "Runs Story loading, graph, executor registry, and variable persistence probes."
    dependsOn("phase1ProjectProbe")
}

tasks.register("phase5LiveProbe") {
    group = "verification"
    description = "Runs Live protocol, debugger trace, and Play Test snapshot probes."
    dependsOn("phase1ProjectProbe")
}

tasks.register<JavaExec>("studio21VerticalSliceProbe") {
    group = "verification"
    description = "Loads the Studio 2.1 acceptance project and verifies named Dialogue exits enter the kingdom/empire Stories."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.project.Studio21VerticalSliceProbe")
    args(layout.projectDirectory.dir(".tooling/2.1-acceptance/DarkGrey-2.1-Acceptance").asFile.absolutePath)
}

tasks.register<JavaExec>("studio21RegressionProbe") {
    group = "verification"
    description = "Runs the full legacy project/dialogue/quest/story/live regression probe against the complete Phase 5 content pack."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.project.ProjectRepositoryProbe")
    args(
        layout.buildDirectory.dir("studio21-regression-probe").get().asFile.absolutePath,
        layout.projectDirectory.dir("build/phase5-content-pack").asFile.absolutePath,
    )
}

tasks.register<JavaExec>("studio21ActorBindingProbe") {
    group = "verification"
    description = "Verifies CustomNPC+ Actor binding uses persistent stored data and survives wrapper recreation."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.compat.customnpcs.CustomNpcActorBindingProbe")
}
