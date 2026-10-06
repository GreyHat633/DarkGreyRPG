from pathlib import Path
p=Path('src/test/java/darkgrey/rpg/nominator/Nominator0323Probe.java')
p.write_text('''package darkgrey.rpg.nominator;

import java.util.*;
import java.nio.file.*;
import java.nio.charset.StandardCharsets;
import net.minecraft.item.*;
import net.minecraft.nbt.*;
import darkgrey.rpg.identity.*;
import darkgrey.rpg.item.identity.*;
import darkgrey.rpg.project.*;

/** Identity lifecycle, exact/fuzzy unbind, persistence and presentation source guards. */
public final class Nominator0323Probe {
    private Nominator0323Probe() {}
    public static void main(String[] args) throws Exception {
        Map<String,ActorDefinition> actors=new LinkedHashMap<String,ActorDefinition>();
        actors.put("hero",new ActorDefinition(2,ActorDefinition.TYPE_INDIVIDUAL,"hero","Hero","",Collections.<String>emptyList(),""));
        actors.put("group",new ActorDefinition(2,ActorDefinition.TYPE_COLLECTIVE,"group","Group","",Collections.<String>emptyList(),""));
        ProjectSnapshot project=new ProjectSnapshot(new ProjectDefinition(1,"probe","Probe"),actors,Collections.emptyMap(),Collections.emptyMap(),Collections.emptyMap(),Collections.emptyMap(),Collections.emptyMap(),darkgrey.rpg.graph.canonical.CanonicalProjectContent.empty());
        NpcIdentitySavedData npc=new NpcIdentitySavedData(); NominatorSavedData selections=new NominatorSavedData();
        UUID a=UUID.randomUUID(),b=UUID.randomUUID();
        require(NominatorService.bindEntity(true,a,"Pig",0,"hero",Arrays.asList("group"),null,project,npc,selections).isAccepted(),"bind A");
        long rev=npc.getRevision();
        require(!NominatorService.bindEntity(true,b,"Pig",0,"hero",Arrays.asList("group"),null,project,npc,selections).isAccepted(),"normal conflict rejected");
        require(npc.getRevision()==rev && npc.getNpcId(b)==null,"conflict atomic");
        require(NominatorService.bindEntity(true,b,"Pig",0,"hero",Arrays.asList("group"),null,true,project,npc,selections).isAccepted(),"transfer B");
        require(npc.getNpcId(a)==null&&"hero".equals(npc.getNpcId(b)),"reverse transfer");
        require(selections.get(a).getIndividualId()==null&&selections.get(a).getGroupIds().contains("group"),"old host groups preserved");
        require(!NominatorService.releaseEntityResource(false,"hero",project,npc,selections).isAccepted(),"permission guard");
        require(NominatorService.releaseEntityResource(true,"hero",project,npc,selections).isAccepted(),"orphan release without entity object");
        require(npc.getHost("hero")==null&&selections.get(b).getIndividualId()==null&&project.getActor("hero")!=null,"orphan metadata cleanup resource kept");
        require("noop".equals(NominatorService.releaseEntityResource(true,"hero",project,npc,selections).getCode()),"already free typed noop");
        NominatorService.bindEntity(true,b,"Pig",0,"hero",Arrays.asList("group"),null,project,npc,selections);
        NominatorService.unbindEntity(true,b,"Pig",0,project,npc,selections);
        require(selections.get(b)==null&&selections.get(a).getGroupIds().contains("group"),"host only unbind");
        selections.addTypeGroup("Pig","group");
        NominatorService.releaseEntityResource(true,"group",project,npc,selections);
        require(selections.get(a)==null&&selections.getTypeGroups("Pig").isEmpty()&&project.getActor("group")!=null,"group world release");
        System.out.println("NPCID_ORPHAN_TRANSFER_HOST_UNBIND_GROUP_RELEASE=PASS");
        Item item=new Item(); Item.itemRegistry.addObject(31000,"probe:token",item);
        ItemStack sa=new ItemStack(item,3,1),sb=new ItemStack(item,1,2);
        NBTTagCompound tag=new NBTTagCompound();tag.setString("name","A");sa.setTagCompound(tag);
        ItemIdentitySavedData data=new ItemIdentitySavedData();
        ItemStackDefinition da=ItemStackDefinition.capture(sa),db=ItemStackDefinition.capture(sb);
        data.bindItem("one",da);data.bindItem("two",da);
        data.addGroupMember("exact",new ItemGroupMember(ItemMatchMode.EXACT,da));
        data.addGroupMember("fuzzy",new ItemGroupMember(ItemMatchMode.FUZZY,da));
        data.addGroupMember("fuzzy",new ItemGroupMember(ItemMatchMode.EXACT,db));
        require(data.matchesGroup("exact",sa)&&!data.matchesGroup("exact",sb)&&data.matchesGroup("fuzzy",sb),"exact fuzzy metadata");
        ItemStack differentNbt=sa.copy();differentNbt.setTagCompound(null);
        require(!data.matchesGroup("exact",differentNbt)&&data.matchesGroup("fuzzy",differentNbt),"NBT equality");
        ItemStack count=sa.copy();count.stackSize=1;require(data.matchesGroup("exact",count),"count ignored");
        long before=data.getRevision();data.transferItem("one",db);
        require(data.getRevision()==before+1&&data.matchesItem("one",sb)&&data.matchesGroup("exact",sa),"atomic item transfer keeps groups");
        before=data.getRevision();require(data.unbindDefinition(sa),"unbind all applicable");
        require(data.getRevision()==before+1&&!data.matchesItem("two",sa)&&data.matchesItem("one",sb)&&data.getGroup("exact").isEmpty()&&data.getGroup("fuzzy").size()==1,"single revision unrelated exact preserved fuzzy rule removed");
        require(sa.stackSize==3&&sa.getTagCompound().equals(tag),"physical stack unchanged");
        require(data.releaseGroup("fuzzy")&&data.getGroup("fuzzy").isEmpty(),"group release");
        NBTTagCompound saved=new NBTTagCompound();data.writeToNBT(saved);ItemIdentitySavedData loaded=new ItemIdentitySavedData();loaded.readFromNBT(saved);
        require(loaded.matchesItem("one",sb)&&loaded.getGroup("fuzzy").isEmpty(),"restart");
        require(loaded.unbindItem("one")&&loaded.getItem("one")==null,"item release");
        System.out.println("ITEM_TRANSFER_EXACT_FUZZY_UNBIND_RELEASE_PERSISTENCE=PASS");
        for (String file:Arrays.asList("GuiRpgButton","GuiCanonicalTaskScreen","GuiCopierTemplates","GuiNominatorInventory","GuiNominatorEntity","NominatorControls")) {
            String s=source("client/gui/"+file+".java");require(s.contains("DgrUiPalette"),"shared palette "+file);
            require(!s.matches("(?s).*0x(?:FFE4D5AE|FF958976|EE40392E|FFF0CD|FFFFD27A|FF806C4E|DDCCAA|FF574C32|FFFFE8A8).*"),"warm literals "+file);
        }
        String browser=source("client/gui/NominatorBrowser.java");require(browser.contains("resourceLabel(r,")&&!browser.contains("top + 20"),"one line resource");
        require(browser.contains("row.source.getDisplayName()"),"global provenance");
        String modal=source("client/gui/NominatorControls.java");require(modal.contains("left=new GuiRpgButton(90")&&modal.contains("right=new GuiRpgButton(91"),"modal button order");
        require(source("client/gui/GuiNominatorInventory.java").contains("UNBIND_SLOT")&&!source("client/gui/GuiNominatorInventory.java").contains("player.closeScreen"),"two slot keep open");
        System.out.println("NOMINATOR_PALETTE_ROWS_MODAL_SOURCE_GUARD=PASS");
    }
    private static String source(String path) throws Exception { return new String(Files.readAllBytes(Paths.get("src/main/java/darkgrey/rpg/"+path)),StandardCharsets.UTF_8); }
    private static void require(boolean value,String message) { if (!value) throw new AssertionError(message); }
}
''',encoding='utf8')
p=Path('build.gradle.kts');s=p.read_text(encoding='utf8');s+='''\n tasks.register<JavaExec>("nominator0323Probe") {
    group = "verification"
    dependsOn(tasks.testClasses)
    classpath = sourceSets.test.get().runtimeClasspath
    mainClass.set("darkgrey.rpg.nominator.Nominator0323Probe")
}
''';p.write_text(s,encoding='utf8')
# Check only files changed for this release, preserving frozen baseline formatting debt.
import subprocess
paths=subprocess.check_output(['git','diff','--name-only','--','src'],text=True).splitlines()
paths+=subprocess.check_output(['git','ls-files','--others','--exclude-standard','--','src'],text=True).splitlines()
paths=[p for p in paths if p.endswith('.java')]
Path('scripts/0323-client-format.gradle').write_text('gradle.projectsEvaluated {\n    rootProject.spotless.java {\n        target '+', '.join("rootProject.file('"+p+"')" for p in paths)+'\n    }\n}\n',encoding='utf8')
p=Path('scripts/verify-0323-freeze.ps1');s=Path('scripts/verify-0322-freeze.ps1').read_text(encoding='utf8').replace('0f1f002aba10a7e1ae12564d6fdda093d5389ef4','c5ee16069ee938fd684af621de56597993c48c4e');p.write_text(s,encoding='utf8')
