package darkgrey.rpg.network.message.entitytools;

import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;

/** Focused strict-codec proof for server-authoritative copier template management. */
public final class CopierTemplateActionCodecProbe {

    private CopierTemplateActionCodecProbe() {}

    public static void main(String[] args) {
        C2SCopierTemplateAction source = new C2SCopierTemplateAction(
            4,
            C2SCopierTemplateAction.Operation.DELETE,
            2,
            5,
            "customnpcs.CustomNpc");
        ByteBuf bytes = Unpooled.buffer();
        source.toBytes(bytes);
        C2SCopierTemplateAction decoded = new C2SCopierTemplateAction();
        decoded.fromBytes(bytes);
        require(decoded.getHotbarSlot() == 4, "slot");
        require(decoded.getOperation() == C2SCopierTemplateAction.Operation.DELETE, "operation");
        require(decoded.getTemplateIndex() == 2 && decoded.getExpectedCount() == 5, "bounds");
        require("customnpcs.CustomNpc".equals(decoded.getExpectedEntityType()), "entity type");
        reject(new Runnable() {

            @Override
            public void run() {
                new C2SCopierTemplateAction(9, C2SCopierTemplateAction.Operation.SELECT, 0, 1, "minecraft:wolf");
            }
        });
        reject(new Runnable() {

            @Override
            public void run() {
                ByteBuf trailing = Unpooled.buffer();
                source.toBytes(trailing);
                trailing.writeByte(1);
                new C2SCopierTemplateAction().fromBytes(trailing);
            }
        });
        System.out.println("COPIER_TEMPLATE_ACTION_CODEC=PASS");
    }

    private static void reject(Runnable action) {
        try {
            action.run();
        } catch (RuntimeException expected) {
            return;
        }
        throw new AssertionError("Invalid copier packet was accepted.");
    }

    private static void require(boolean condition, String label) {
        if (!condition) throw new AssertionError("Copier packet probe failed: " + label);
    }
}
