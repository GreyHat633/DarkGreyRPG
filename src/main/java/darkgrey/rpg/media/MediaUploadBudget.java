package darkgrey.rpg.media;

/** Reservations include queued, decoding and upload-ready images. */
final class MediaUploadBudget {

    private int count;
    private long pixels;

    synchronized boolean reserve(long value) {
        if (value <= 0 || count >= 4 || pixels + value > 16777216L) return false;
        count++;
        pixels += value;
        return true;
    }

    synchronized void release(long value) {
        if (count <= 0 || pixels < value) throw new IllegalStateException("Unbalanced image reservation");
        count--;
        pixels -= value;
    }

    synchronized int count() {
        return count;
    }

    synchronized long pixels() {
        return pixels;
    }
}
