package darkgrey.rpg.client.session;

/** Pure reading-copy contract: bounded, isolated, no author/runtime data dependencies. */
public final class DialogueBacklog0332Probe {

    public static void main(String[] args) {
        DialogueBacklog backlog = new DialogueBacklog();
        backlog.upsert("server-a/player-a", "run1/node/page1/1", "旅人", "半");
        check(
            backlog.entries("server-a/player-a")
                .get(0).text.equals("半"),
            "no future text");
        backlog.upsert("server-a/player-a", "run1/node/page1/1", "旅人", "半句显示");
        backlog.upsert("server-a/player-a", "run1/node/page1/1", "旅人", "半");
        check(
            backlog.entries("server-a/player-a")
                .size() == 1,
            "same line upsert");
        check(
            backlog.entries("server-a/player-a")
                .get(0).text.equals("半句显示"),
            "reconnect keeps read suffix");
        backlog.upsert("server-b/player-a", "run1/node/page1/1", "旅人", "B");
        backlog.upsert("server-a/player-b", "run1/node/page1/1", "旅人", "其他账号");
        check(
            backlog.entries("server-a/player-a")
                .size() == 1,
            "A B A preserved");
        backlog.upsert("server-a/player-a", "run2/node/page1/1", "旅人", "半句显示");
        check(
            backlog.entries("server-a/player-a")
                .size() == 2,
            "new run repeats text");
        for (int i = 2; i < 251; i++) backlog.upsert("server-a/player-a", "id" + i, "", "text" + i);
        check(
            backlog.entries("server-a/player-a")
                .size() == 250,
            "250 bound");
        check(
            backlog.entries("server-a/player-a")
                .get(0).identity.equals("run2/node/page1/1"),
            "oldest removed");
        backlog.clear("server-a/player-a");
        check(
            backlog.entries("server-a/player-a")
                .isEmpty(),
            "local clear");
        check(
            backlog.entries("server-b/player-a")
                .size() == 1,
            "other server retained");
        check(
            backlog.entries("server-a/player-b")
                .size() == 1,
            "other account retained");
        System.out.println("DIALOGUE_BACKLOG_0332=PASS");
    }

    private static void check(boolean condition, String label) {
        if (!condition) throw new AssertionError(label);
    }
}
