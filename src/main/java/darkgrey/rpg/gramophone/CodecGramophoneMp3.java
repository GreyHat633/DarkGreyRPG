package darkgrey.rpg.gramophone;

import java.io.BufferedInputStream;
import java.net.URL;
import javazoom.jl.decoder.Bitstream;
import javazoom.jl.decoder.Decoder;
import javazoom.jl.decoder.Header;
import javazoom.jl.decoder.SampleBuffer;

import javax.sound.sampled.AudioFormat;

import paulscode.sound.ICodec;
import paulscode.sound.SoundBuffer;

/** Bounded frame decoding for the gramophone mixer and media inspection workers. */
public final class CodecGramophoneMp3 implements ICodec {

    private Bitstream stream;
    private Decoder decoder;
    private AudioFormat format;
    private SoundBuffer first;
    private boolean ended;
    private boolean failed;

    public boolean failed() {
        return failed;
    }

    private final boolean bigEndian = java.nio.ByteOrder.nativeOrder() == java.nio.ByteOrder.BIG_ENDIAN;

    // Like CodecJOrbis, emit native PCM. OpenAL requests reverseByteOrder for
    // codecs with big-endian input; these already-decoded samples must not swap.
    @Override
    public void reverseByteOrder(boolean reverse) {}

    @Override
    public boolean initialize(URL url) {
        cleanup();
        ended = false;
        failed = false;
        try {
            if (!"file".equals(url.getProtocol())) return false;
            stream = new Bitstream(new BufferedInputStream(url.openStream()));
            decoder = new Decoder();
            first = decode();
            double skip = url.getRef() == null ? 0 : Double.parseDouble(url.getRef());
            if (!Double.isFinite(skip) || skip < 0 || skip > 900)
                throw new IllegalArgumentException("Invalid seek position");
            double elapsed = 0;
            while (first != null && elapsed < skip) {
                elapsed += first.audioData.length / (format.getFrameSize() * (double) format.getFrameRate());
                first = decode();
            }
            return first != null;
        } catch (Exception exception) {
            cleanup();
            failed = true;
            return false;
        }
    }

    private SoundBuffer decode() {
        if (stream == null || ended) return null;
        try {
            Header header = stream.readFrame();
            if (header == null) {
                ended = true;
                return null;
            }
            SampleBuffer samples = (SampleBuffer) decoder.decodeFrame(header, stream);
            if (format != null && (format.getSampleRate() != samples.getSampleFrequency()
                || format.getChannels() != samples.getChannelCount()))
                throw new IllegalArgumentException("MP3 format changes within the stream");
            format = new AudioFormat(samples.getSampleFrequency(), 16, samples.getChannelCount(), true, bigEndian);
            short[] values = samples.getBuffer();
            byte[] bytes = new byte[samples.getBufferLength() * 2];
            for (int i = 0; i < samples.getBufferLength(); i++) {
                bytes[i * 2 + (bigEndian ? 1 : 0)] = (byte) values[i];
                bytes[i * 2 + (bigEndian ? 0 : 1)] = (byte) (values[i] >> 8);
            }
            stream.closeFrame();
            return new SoundBuffer(bytes, format);
        } catch (Exception exception) {
            ended = true;
            failed = true;
            return null;
        }
    }

    @Override
    public boolean initialized() {
        return stream != null && format != null;
    }

    @Override
    public SoundBuffer read() {
        if (first != null) {
            SoundBuffer result = first;
            first = null;
            return result;
        }
        return decode();
    }

    @Override
    public SoundBuffer readAll() {
        throw new UnsupportedOperationException("Gramophone MP3 is streaming only");
    }

    @Override
    public boolean endOfStream() {
        return ended;
    }

    @Override
    public AudioFormat getAudioFormat() {
        return format;
    }

    @Override
    public void cleanup() {
        if (stream != null) try {
            stream.close();
        } catch (Exception ignored) {}
        stream = null;
        decoder = null;
        first = null;
        format = null;
        ended = true;
    }
}
