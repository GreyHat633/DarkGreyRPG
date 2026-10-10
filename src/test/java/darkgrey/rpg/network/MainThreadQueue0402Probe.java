package darkgrey.rpg.network;

import java.util.ArrayList;
import java.util.List;

import net.minecraft.nbt.NBTTagCompound;

import darkgrey.rpg.network.message.canonical.TaskPresentationPage;
import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;

public final class MainThreadQueue0402Probe {

    private MainThreadQueue0402Probe() {}

    public static void main(String[] args) throws Exception {
        pageFailureCorrelation();
        long[] now = { 0 };
        List<Integer> executed = new ArrayList<>();
        List<RuntimeException> failures = new ArrayList<>();
        int[] cleanup = { 0 };
        MainThreadQueue queue = new MainThreadQueue(4, 2, 10, () -> now[0], failures::add);
        MainThreadQueue.Scope owner = new MainThreadQueue.Scope();
        for (int i = 0; i < 4; i++) {
            final int id = i;
            require(queue.offer(owner, () -> true, () -> executed.add(id), reason -> cleanup[0]++), "admitted");
        }
        require(!queue.offer(owner, () -> true, () -> executed.add(99), reason -> cleanup[0]++), "full rejected");
        require(cleanup[0] == 1, "full release once");
        queue.drain();
        require(executed.size() == 2 && queue.metrics()[0] == 2, "count budget");
        queue.drain();
        require(executed.equals(java.util.Arrays.asList(0, 1, 2, 3)), "FIFO preserved");
        queue.offer(owner, () -> true, () -> now[0] += 15, reason -> cleanup[0]++);
        queue.offer(owner, () -> true, () -> executed.add(4), reason -> cleanup[0]++);
        queue.drain();
        require(queue.metrics()[0] == 1 && queue.metrics()[8] == 1, "time budget after slow task");
        queue.drain();
        require(executed.get(4) == 4, "retained work resumes");
        queue.offer(
            owner,
            () -> true,
            () -> { throw new IllegalStateException("single request"); },
            reason -> cleanup[0]++);
        queue.offer(owner, () -> true, () -> executed.add(5), reason -> cleanup[0]++);
        queue.drain();
        require(failures.size() == 1 && executed.get(5) == 5, "failure isolated and cleaned");
        queue.offer(owner, () -> true, () -> { throw new AssertionError("fatal"); }, reason -> cleanup[0]++);
        try {
            queue.drain();
            throw new IllegalStateException("Error swallowed");
        } catch (AssertionError expected) {
            require("fatal".equals(expected.getMessage()), "Error propagated");
        }
        for (int i = 0; i < 20; i++) {
            MainThreadQueue.Scope old = new MainThreadQueue.Scope();
            queue.offer(old, () -> true, () -> executed.add(99), reason -> cleanup[0]++);
            queue.retire(old);
            require(
                !queue.offer(old, () -> true, () -> executed.add(99), reason -> cleanup[0]++),
                "late callback rejected");
            queue.drain();
        }
        require(!executed.contains(99), "old world never executed");
        queue.offer(owner, () -> false, () -> executed.add(99), reason -> cleanup[0]++);
        queue.drain();
        require(!executed.contains(99), "connection replaced");
        require(cleanup[0] == 43, "all rejected/cancelled/failed callbacks once: " + cleanup[0]);
        MainThreadQueue reserved = new MainThreadQueue(1, 2, 10, () -> now[0], failures::add);
        List<Integer> order = new ArrayList<>();
        require(reserved.offer(owner, () -> true, () -> order.add(1), reason -> {}), "ordinary admitted");
        require(!reserved.offer(owner, () -> true, () -> order.add(99), reason -> {}), "ordinary full");
        require(reserved.offerCompletion(owner, () -> true, () -> order.add(2), reason -> {}), "completion reserve");
        reserved.drain();
        require(order.equals(java.util.Arrays.asList(1, 2)), "completion keeps FIFO");
        for (int i = 0; i < 256; i++) require(
            reserved.offerCompletion(owner, () -> true, () -> {}, reason -> cleanup[0]++),
            "completion admitted");
        require(!reserved.offerCompletion(owner, () -> true, () -> {}, reason -> cleanup[0]++), "completion bounded");
        reserved.retire(owner);
        require(reserved.metrics()[0] == 0, "reserved cleanup");
        System.out.println(
            "MAIN_THREAD_QUEUE_0402=PASS count time FIFO full retire late-callback recoverable-failure fatal-error scopes=20");
    }

    private static void pageFailureCorrelation() {
        NBTTagCompound request = new NBTTagCompound();
        request.setLong("sequence", 40201L);
        request.setInteger("dimension", -1);
        request.setInteger("operation", 1);
        request.setInteger("cursor", 20);
        request.setString("story", "ST-AAAA-BBBB-CCCC-DDDD");
        request.setString("placement", "work");
        request.setString("objective", "submit");
        request.setLong("activation", 402L);
        NBTTagCompound before = (NBTTagCompound) request.copy();
        TaskPresentationPage failure = TaskPresentationPage.failureResponse(request, "繁忙，请稍后重试");
        ByteBuf bytes = Unpooled.buffer();
        try {
            failure.toBytes(bytes);
            TaskPresentationPage received = new TaskPresentationPage();
            received.fromBytes(bytes);
            require(
                received.response && received.data.getLong("sequence") == 40201L
                    && "繁忙，请稍后重试".equals(received.data.getString("error")),
                "Correlated failure survives wire codec");
            require(
                darkgrey.rpg.client.TaskPresentationPages.identity(request)
                    .equals(darkgrey.rpg.client.TaskPresentationPages.identity(received.data)),
                "Exact pending page key");
            require(request.equals(before), "Reject never rewrites request identity");
        } finally {
            bytes.release();
        }
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
}
