package darkgrey.rpg.runtime;

import java.util.List;

import net.minecraft.entity.Entity;
import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.util.AxisAlignedBB;
import net.minecraft.util.MovingObjectPosition;
import net.minecraft.util.Vec3;

public final class EntityTargeting {

    private EntityTargeting() {}

    public static Entity findLookedAtEntity(EntityPlayer player, double maximumDistance) {
        Vec3 start = Vec3.createVectorHelper(player.posX, player.posY + player.getEyeHeight(), player.posZ);
        Vec3 direction = player.getLook(1.0F);
        Vec3 end = start.addVector(
            direction.xCoord * maximumDistance,
            direction.yCoord * maximumDistance,
            direction.zCoord * maximumDistance);

        MovingObjectPosition blockHit = player.worldObj.rayTraceBlocks(start, end);
        double allowedDistance = maximumDistance;
        if (blockHit != null && blockHit.hitVec != null) {
            allowedDistance = start.distanceTo(blockHit.hitVec);
        }

        AxisAlignedBB searchBounds = player.boundingBox
            .addCoord(
                direction.xCoord * maximumDistance,
                direction.yCoord * maximumDistance,
                direction.zCoord * maximumDistance)
            .expand(1.0D, 1.0D, 1.0D);
        @SuppressWarnings("unchecked")
        List<Entity> candidates = player.worldObj.getEntitiesWithinAABBExcludingEntity(player, searchBounds);

        Entity closest = null;
        double closestDistance = allowedDistance;
        for (Entity candidate : candidates) {
            if (!candidate.canBeCollidedWith()) {
                continue;
            }
            float border = candidate.getCollisionBorderSize();
            AxisAlignedBB bounds = candidate.boundingBox.expand(border, border, border);
            MovingObjectPosition intercept = bounds.calculateIntercept(start, end);
            if (bounds.isVecInside(start)) {
                if (closestDistance >= 0.0D) {
                    closest = candidate;
                    closestDistance = 0.0D;
                }
            } else if (intercept != null) {
                double distance = start.distanceTo(intercept.hitVec);
                if (distance < closestDistance || closestDistance == 0.0D) {
                    closest = candidate;
                    closestDistance = distance;
                }
            }
        }
        return closest;
    }
}
