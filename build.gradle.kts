plugins {
    id("com.gtnewhorizons.gtnhconvention")
}

tasks.register<JavaExec>("namespacedResourceLoadingProbe") {
    dependsOn(tasks.testClasses)
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("darkgrey.rpg.identity.NamespacedResourceLoadingProbe")
    args("E:/Java/MinecraftMod/DarkGrey_RPG/.tooling/0.3.2.0_B4/resource-loader")
}

tasks.register<JavaExec>("dgrResourceIdProbe") {
    dependsOn(tasks.testClasses)
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("darkgrey.rpg.identity.DgrResourceIdProbe")
}

tasks.register<JavaExec>("b4ExternalPackageReferencesProbe") {
    group = "verification"
    dependsOn(tasks.testClasses)
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("darkgrey.rpg.project.packages.B4ExternalPackageReferencesProbe")
}

tasks.register<JavaExec>("b4ExternalArchiveSetProbe") {
    group = "verification"
    dependsOn(tasks.testClasses)
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("darkgrey.rpg.project.packages.B4ExternalArchiveSetProbe")
    args(layout.projectDirectory.dir(".tooling/0.3.2.0_B4/external-packages").asFile.absolutePath)
}

tasks.register<JavaExec>("canonicalStoryCommandProbe") {
    dependsOn(tasks.testClasses)
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("darkgrey.rpg.command.CanonicalStoryCommandProbe")
}

tasks.register<JavaExec>("canonicalActorArbitrationProbe") {
    group = "verification"
    dependsOn(tasks.named("testClasses"))
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("darkgrey.rpg.story.canonical.server.CanonicalActorArbitrationProbe")
}

tasks.register<JavaExec>("canonicalStoryChooserCodecProbe") {
    group = "verification"
    dependsOn(tasks.named("testClasses"))
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("darkgrey.rpg.network.message.canonical.CanonicalStoryChooserCodecProbe")
}

tasks.register<JavaExec>("b4NetworkDiscriminatorProbe") {
    group = "verification"
    description = "Validates the shared FML packet discriminator registry across all B4 network modules."
    dependsOn(tasks.named("testClasses"))
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("darkgrey.rpg.network.B4NetworkDiscriminatorProbe")
    args(layout.projectDirectory.asFile.absolutePath)
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

tasks.register<JavaExec>("canonicalGraphResourceProbe") {
    group = "verification"
    description = "Runs the strict canonical Story/Session/Task graph resource loader probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.graph.canonical.CanonicalGraphResourceLoaderProbe")
    args(layout.buildDirectory.dir("canonical-graph-resource-probe").get().asFile.absolutePath)
}

tasks.register<JavaExec>("canonicalStoryMembershipProbe") {
    group = "verification"
    description = "Runs the strict canonical Story membership manifest loader probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.graph.canonical.CanonicalStoryMembershipLoaderProbe")
    args(layout.buildDirectory.dir("canonical-story-membership-probe").get().asFile.absolutePath)
}

tasks.register<JavaExec>("canonicalProjectContentProbe") {
    group = "verification"
    description = "Runs the strict canonical project-content snapshot loader probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.graph.canonical.CanonicalProjectContentLoaderProbe")
    args(layout.buildDirectory.dir("canonical-project-content-probe").get().asFile.absolutePath)
}

tasks.register<JavaExec>("canonicalProjectRepositoryProbe") {
    group = "verification"
    description = "Verifies canonical project content participates in atomic ProjectRepository reloads."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.project.CanonicalProjectRepositoryProbe")
    args(layout.buildDirectory.dir("canonical-project-repository-probe").get().asFile.absolutePath)
}

tasks.register<JavaExec>("canonicalSessionRuntimeProbe") {
    group = "verification"
    description = "Runs the canonical Session flow runtime and snapshot probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.session.runtime.CanonicalSessionRuntimeProbe")
}

tasks.register<JavaExec>("canonicalTaskRuntimeProbe") {
    group = "verification"
    description = "Runs the canonical Task logic runtime and immutable snapshot probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.task.runtime.CanonicalTaskRuntimeProbe")
}

tasks.register<JavaExec>("canonicalStoryRuntimeProbe") {
    group = "verification"
    description = "Runs the canonical Stage 5 single-cursor Story runtime probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.story.canonical.runtime.CanonicalStoryRuntimeProbe")
}

tasks.register<JavaExec>("canonicalStoryActionConfigurationProbe") {
    group = "verification"
    description = "Runs the strict Stage 5 Story action configuration parser probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.story.canonical.runtime.CanonicalStoryActionConfigurationProbe")
}

tasks.register<JavaExec>("canonicalStoryInstanceProbe") {
    group = "verification"
    description = "Runs the canonical Stage 5 Story instance identity and NBT persistence probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceProbe")
}

tasks.register<JavaExec>("canonicalStoryServerServiceProbe") {
    group = "verification"
    description = "Runs the canonical Stage 5 trigger and aggregate Story service probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.story.canonical.server.CanonicalStoryServerServiceProbe")
}

tasks.register<JavaExec>("canonicalStoryForgeCoordinatorProbe") {
    group = "verification"
    description = "Runs the Stage 5 Forge Story coordinator route and failure cleanup probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.story.canonical.forge.CanonicalStoryForgeCoordinatorProbe")
}

tasks.register<JavaExec>("canonicalTaskForgeProbe") {
    group = "verification"
    description = "Runs the bounded Forge canonical Task adapter/manager probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.task.forge.CanonicalTaskForgeProbe")
}

tasks.register<JavaExec>("canonicalTaskInstanceProbe") {
    group = "verification"
    description = "Runs the server-neutral TaskInstance lifecycle and strict NBT restart probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.task.instance.CanonicalTaskInstanceProbe")
}

tasks.register<JavaExec>("canonicalTaskJournalProjectionProbe") {
    group = "verification"
    description = "Runs the pure canonical Task Journal projection probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.task.journal.CanonicalTaskJournalProjectionProbe")
}

tasks.register<JavaExec>("canonicalTaskJournalIntegrationProbe") {
    group = "verification"
    description = "Runs the Stage 4 canonical-to-legacy Quest Journal integration probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.quest.runtime.CanonicalTaskJournalIntegrationProbe")
}

tasks.register<JavaExec>("canonicalTaskStage4SurfaceProbe") {
    group = "verification"
    description = "Runs the Stage 4 Task command and Journal-tab structural probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.command.CanonicalTaskStage4SurfaceProbe")
}

tasks.register<JavaExec>("canonicalTaskEventPersistenceProbe") {
    group = "verification"
    description = "Runs the canonical Task SavedData persistence and indexed event probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.task.persistence.CanonicalTaskEventPersistenceProbe")
}

tasks.register<JavaExec>("canonicalSessionInstanceProbe") {
    group = "verification"
    description = "Runs the server-neutral Session instance/store and strict NBT restart probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.session.instance.CanonicalSessionInstanceProbe")
}

tasks.register<JavaExec>("canonicalSessionNetworkCodecProbe") {
    group = "verification"
    description = "Runs the strict canonical Session network DTO and codec probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.network.message.canonical.CanonicalSessionNetworkCodecProbe")
}

tasks.register<JavaExec>("canonicalSessionSavedDataProbe") {
    group = "verification"
    description = "Runs the Forge WorldSavedData canonical Session persistence probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.session.persistence.CanonicalSessionSavedDataProbe")
}

tasks.register<JavaExec>("canonicalSessionServerServiceProbe") {
    group = "verification"
    description = "Runs the canonical Session server-neutral orchestration and projection probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.session.server.CanonicalSessionServerServiceProbe")
}

tasks.register<JavaExec>("canonicalSessionClientModelProbe") {
    group = "verification"
    description = "Runs the canonical Session client identity and stable-option probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.client.session.CanonicalSessionClientModelProbe")
}

tasks.register<JavaExec>("canonicalSessionForgeRoutingProbe") {
    group = "verification"
    description = "Runs the canonical Forge Frame/Close routing seam probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.session.forge.CanonicalSessionForgeRoutingProbe")
}

tasks.register<JavaExec>("canonicalSessionCommandProbe") {
    group = "verification"
    description = "Runs the canonical Session command usage and completion seam probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.command.CanonicalSessionCommandProbe")
}

tasks.register<JavaExec>("canonicalStorySessionCompletionRouterProbe") {
    group = "verification"
    description = "Runs the pure canonical Story Flow Session-completion router probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.story.canonical.CanonicalStorySessionCompletionRouterProbe")
}

tasks.register<JavaExec>("npcIdentitySavedDataProbe") {
    group = "verification"
    description = "Runs the 0.3.1.0 external unique NPC identity registry and restart probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.identity.NpcIdentitySavedDataProbe")
}

tasks.register<JavaExec>("entityDgrIdentityResolverProbe") {
    group = "verification"
    description = "Runs the external NPC, nominator, group-combination, and precedence probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.identity.EntityDgrIdentityResolverProbe")
}

tasks.register<JavaExec>("itemIdentitySavedDataProbe") {
    group = "verification"
    description = "Runs the 0.3.1.0 Item ID and exact/fuzzy Group persistence probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.item.identity.ItemIdentitySavedDataProbe")
}

tasks.register<JavaExec>("nominatorStage4Probe") {
    group = "verification"
    description = "Runs the Stage 4 nominator search, permission, conflict, and multi-group probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.nominator.NominatorStage4Probe")
}

tasks.register<JavaExec>("nominatorItemContainerProbe") {
    group = "verification"
    description = "Runs the Item Nominator target-slot movement and return accounting probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.nominator.container.NominatorItemContainerProbe")
}

tasks.register<JavaExec>("entityToolsStage5Probe") {
    group = "verification"
    description = "Runs the pure Stage 5 Copier and Storage Box core probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.entitytools.EntityToolsStage5Probe")
}

tasks.register<JavaExec>("copierTemplateActionCodecProbe") {
    group = "verification"
    description = "Runs the strict Copier template-management packet codec probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.network.message.entitytools.CopierTemplateActionCodecProbe")
}

tasks.register<JavaExec>("playerRpgSavedDataProbe") {
    group = "verification"
    description = "Runs the 0.3.1.0 UUID-isolated player fact, choice, and reward persistence probe."
    dependsOn("testClasses")
    classpath = files(
        layout.buildDirectory.dir("classes/java/test"),
        layout.buildDirectory.dir("classes/java/main"),
        layout.buildDirectory.dir("classes/java/patchedMc"),
        layout.buildDirectory.dir("resources/main"),
        layout.buildDirectory.dir("resources/patchedMc"),
    ) + configurations.getByName("testRuntimeClasspath")
    mainClass.set("darkgrey.rpg.player.PlayerRpgSavedDataProbe")
}

tasks.register<JavaExec>("actorSchema3ProjectRepositoryProbe") {
    group = "verification"
    description = "Runs the 0.3.1 identity-only Actor runtime/reload probe."
    dependsOn(tasks.named("testClasses"))
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("darkgrey.rpg.project.ActorSchema3ProjectRepositoryProbe")
    args(layout.buildDirectory.dir("actor-schema3-project-repository-probe").get().asFile.absolutePath)
}

tasks.register<JavaExec>("storyPackageLoaderProbe") {
    group = "verification"
    description = "Runs the Stage 3 Story Package load/replace/rollback/isolation probe."
    dependsOn(tasks.named("testClasses"))
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("darkgrey.rpg.project.packages.StoryPackageLoaderProbe")
}

tasks.register<JavaExec>("dgrsPackageRuntimeProbe") {
    group = "verification"
    description = "Loads one Studio-produced DGRS through the Java package and Task runtime path."
    dependsOn(tasks.named("testClasses"))
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("darkgrey.rpg.project.packages.DgrsPackageRuntimeProbe")
    notCompatibleWithConfigurationCache("The external DGRS path is supplied only when this live probe executes.")
    doFirst {
        val packagePath = providers.gradleProperty("dgrsPath").orNull
            ?: throw GradleException("Pass -PdgrsPath=<absolute .dgrs path>")
        args(packagePath, layout.buildDirectory.dir("dgrs-runtime-probe").get().asFile.absolutePath)
    }
}

tasks.register<JavaExec>("dgrsArchiveReaderProbe") {
    group = "verification"
    description = "Runs the detached, strict DGRS archive reader probe."
    dependsOn(tasks.named("testClasses"))
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("darkgrey.rpg.project.packages.DgrsArchiveReaderProbe")
    args(layout.buildDirectory.dir("dgrs-archive-reader-probe").get().asFile.absolutePath)
}

tasks.register<JavaExec>("storyPackageGenerationProbe") {
    group = "verification"
    description = "Runs the B3 package fingerprint, generation delta, and persisted registry probe."
    dependsOn(tasks.named("testClasses"))
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("darkgrey.rpg.project.packages.StoryPackageGenerationProbe")
}


tasks.register<JavaExec>("offlineOriginProbe") {
    group = "verification"
    dependsOn(tasks.testClasses)
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("darkgrey.rpg.project.packages.OfflineOriginProbe")
}

tasks.register<JavaExec>("creatorUxProbe") {
    group = "verification"
    dependsOn(tasks.testClasses)
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("darkgrey.rpg.creator.CreatorUxProbe")
}

tasks.register<JavaExec>("creatorNetworkDiscriminatorProbe") {
    group = "verification"
    dependsOn(tasks.testClasses)
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("darkgrey.rpg.creator.CreatorNetworkDiscriminatorProbe")
}


tasks.register<JavaExec>("presentation0322Probe") {
    group = "verification"
    dependsOn(tasks.testClasses)
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("darkgrey.rpg.client.Presentation0322Probe")
}

 tasks.register<JavaExec>("nominator0323Probe") {
    group = "verification"
    dependsOn(tasks.testClasses)
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("darkgrey.rpg.nominator.Nominator0323Probe")
}

tasks.register<JavaExec>("smoothScroll0324Probe") {
    group = "verification"
    dependsOn(tasks.named("testClasses"))
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("darkgrey.rpg.client.gui.SmoothScroll0324Probe")
}

tasks.register<JavaExec>("utilityWindow0324Probe") {
    group = "verification"
    dependsOn(tasks.named("testClasses"))
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("darkgrey.rpg.client.gui.UtilityWindow0324Probe")
}

tasks.register<JavaExec>("objective0324Probe") {
    group = "verification"
    dependsOn(tasks.named("testClasses"))
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("darkgrey.rpg.client.gui.Objective0324Probe")
    args(layout.projectDirectory.dir("PLAN/0.3.2.4/evidence").asFile.absolutePath)
}
