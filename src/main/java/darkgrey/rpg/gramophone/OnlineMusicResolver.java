package darkgrey.rpg.gramophone;

import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.net.HttpURLConnection;
import java.net.InetAddress;
import java.net.URI;
import java.net.URL;
import java.net.URLEncoder;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Locale;

import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

/** Anonymous provider adapters. No cookies, arbitrary URL proxy, or TLS bypass. */
public final class OnlineMusicResolver {

    public static final int MAX_BYTES = 32 * 1024 * 1024;

    private OnlineMusicResolver() {}

    /** Run on a worker: supported share redirects are normalized before persisting a source. */
    public static OnlineMusicSource normalize(String text) throws IOException {
        if (text == null || text.length() > 2048) throw new IOException("分享链接为空或过长");
        String address = text.trim();
        if (address.startsWith("http://")) address = "https://" + address.substring(7);
        try {
            return OnlineMusicSource.parse(address);
        } catch (IllegalArgumentException ignored) {}
        final URI uri;
        try {
            uri = URI.create(address);
        } catch (IllegalArgumentException e) {
            throw new IOException("分享链接格式无效");
        }
        String host = uri.getHost(), provider;
        if ("163cn.tv".equalsIgnoreCase(host)) provider = "netease";
        else if ("c6.y.qq.com".equalsIgnoreCase(host) && "/base/fcgi-bin/u".equals(uri.getPath())) provider = "qq";
        else throw new IOException("不支持此分享地址，请使用 QQ / 网易云单曲详情链接");
        HttpURLConnection connection = open(address, provider);
        try {
            return OnlineMusicSource.parse(
                connection.getURL()
                    .toString());
        } catch (IllegalArgumentException e) {
            throw new IOException("分享链接未重定向至可识别单曲，请复制歌曲详情链接");
        } finally {
            connection.disconnect();
        }
    }

    public static Path download(OnlineMusicSource source, Path directory) throws IOException {
        String address;
        double expectedSeconds = 0;
        if (source.provider.equals("netease")) {
            try {
                JsonObject detail = new JsonParser().parse(
                    new String(
                        fetchBytes(
                            "https://music.163.com/api/song/detail/?id=" + source.key + "&ids=%5B" + source.key + "%5D",
                            "netease",
                            1024 * 1024),
                        "UTF-8"))
                    .getAsJsonObject()
                    .getAsJsonArray("songs")
                    .get(0)
                    .getAsJsonObject();
                if (detail.get("fee")
                    .getAsInt() != 0) throw new IOException("此网易云歌曲不是当前匿名完整播放范围内的免费曲目");
                expectedSeconds = detail.get("duration")
                    .getAsDouble() / 1000.0;
            } catch (RuntimeException exception) {
                throw new IOException("无法确认网易云曲目免费播放状态", exception);
            }
            address = "https://music.163.com/song/media/outer/url?id=" + source.key + ".mp3";
        } else {
            String detailQuery = "{\"req_0\":{\"module\":\"music.pf_song_detail_svr\",\"method\":\"get_song_detail_yqq\",\"param\":{\"song_mid\":\""
                + source.key
                + "\"}},\"comm\":{\"uin\":0,\"format\":\"json\",\"ct\":24,\"cv\":0}}";
            final String mediaMid;
            try {
                JsonObject detail = new JsonParser()
                    .parse(
                        new String(
                            fetchBytes(
                                "https://u.y.qq.com/cgi-bin/musicu.fcg?data=" + URLEncoder.encode(detailQuery, "UTF-8"),
                                "qq",
                                1024 * 1024),
                            "UTF-8"))
                    .getAsJsonObject()
                    .getAsJsonObject("req_0")
                    .getAsJsonObject("data")
                    .getAsJsonObject("track_info");
                if (detail.getAsJsonObject("pay")
                    .get("pay_play")
                    .getAsInt() != 0) throw new IOException("此 QQ 歌曲不是当前匿名完整播放范围内的免费曲目");
                mediaMid = detail.getAsJsonObject("file")
                    .get("media_mid")
                    .getAsString();
                if (!mediaMid.matches("[A-Za-z0-9]{14}")) throw new IOException("平台媒体标识无效");
                expectedSeconds = detail.get("interval")
                    .getAsDouble();
            } catch (RuntimeException exception) {
                throw new IOException("无法确认 QQ 曲目免费播放状态", exception);
            }
            String data = "{\"req_0\":{\"module\":\"vkey.GetVkeyServer\",\"method\":\"CgiGetVkey\",\"param\":{"
                + "\"guid\":\"1000000000\",\"songmid\":[\""
                + source.key
                + "\"],\"filename\":[\"M500"
                + mediaMid
                + ".mp3\"],\"songtype\":[0],\"uin\":\"0\",\"loginflag\":1,\"platform\":\"20\"}},"
                + "\"comm\":{\"uin\":0,\"format\":\"json\",\"ct\":24,\"cv\":0}}";
            byte[] response = fetchBytes(
                "https://u.y.qq.com/cgi-bin/musicu.fcg?data=" + URLEncoder.encode(data, "UTF-8"),
                source.provider,
                1024 * 1024);
            try {
                JsonObject root = new JsonParser().parse(new String(response, "UTF-8"))
                    .getAsJsonObject();
                JsonObject result = root.getAsJsonObject("req_0");
                if (result.get("code")
                    .getAsInt() != 0) throw new IOException("QQ 音乐当前不允许匿名解析此曲目");
                JsonObject payload = result.getAsJsonObject("data");
                String path = payload.getAsJsonArray("midurlinfo")
                    .get(0)
                    .getAsJsonObject()
                    .get("purl")
                    .getAsString();
                if (path.isEmpty()) throw new IOException("QQ 音乐未提供可播放来源（可能受登录、会员或地区限制）");
                // Do not silently loop a provider's preview clip as a complete track.
                if (path.toLowerCase(Locale.ROOT)
                    .contains("trial")) throw new IOException("此来源为试听片段，不能作为完整背景音乐");
                com.google.gson.JsonArray endpoints = payload.getAsJsonArray("sip");
                address = endpoints.get(endpoints.size() - 1)
                    .getAsString() + path;
            } catch (RuntimeException exception) {
                throw new IOException("QQ 音乐返回了无法识别的解析结果", exception);
            }
        }
        if (expectedSeconds <= 0 || expectedSeconds > 900) throw new IOException("仅支持不超过 15 分钟的完整单曲");
        GramophoneFiles.directory(directory);
        Path temporary = Files.createTempFile(directory, "track-", ".dgrmp3");
        boolean complete = false;
        try {
            HttpURLConnection connection = open(address, source.provider);
            try (InputStream input = connection.getInputStream();
                java.io.OutputStream output = Files.newOutputStream(temporary)) {
                String type = connection.getContentType();
                if (type == null || !(type.toLowerCase(Locale.ROOT)
                    .startsWith("audio/")
                    || type.toLowerCase(Locale.ROOT)
                        .startsWith("application/octet-stream")))
                    throw new IOException("平台未返回音频，歌曲可能下架或受限");
                copy(input, output, MAX_BYTES);
            } finally {
                connection.disconnect();
            }
            if (expectedSeconds > 0) {
                CodecGramophoneMp3 codec = new CodecGramophoneMp3();
                try {
                    if (!codec.initialize(
                        temporary.toUri()
                            .toURL()))
                        throw new IOException("平台音频无法解码");
                    javax.sound.sampled.AudioFormat format = codec.getAudioFormat();
                    long pcm = 0;
                    paulscode.sound.SoundBuffer frame;
                    while ((frame = codec.read()) != null) {
                        if (Thread.currentThread()
                            .isInterrupted()) throw new IOException("媒体校验已取消");
                        pcm += frame.audioData.length;
                    }
                    double seconds = pcm / (format.getFrameSize() * format.getFrameRate());
                    if (codec.failed()) throw new IOException("音频解码失败，文件可能损坏");
                    if (Math.abs(seconds - expectedSeconds) > 5) throw new IOException("音频时长与整曲不符，可能为试听或不完整响应");
                } finally {
                    codec.cleanup();
                }
            }
            complete = true;
            return temporary;
        } finally {
            if (!complete) Files.deleteIfExists(temporary);
        }
    }

    private static byte[] fetchBytes(String address, String provider, int limit) throws IOException {
        HttpURLConnection connection = open(address, provider);
        try (InputStream input = connection.getInputStream();
            ByteArrayOutputStream output = new ByteArrayOutputStream()) {
            copy(input, output, limit);
            return output.toByteArray();
        } finally {
            connection.disconnect();
        }
    }

    private static void copy(InputStream input, java.io.OutputStream output, int maximum) throws IOException {
        byte[] buffer = new byte[16384];
        int count;
        long total = 0;
        long deadline = System.nanoTime() + java.util.concurrent.TimeUnit.SECONDS.toNanos(60);
        while ((count = input.read(buffer)) >= 0) {
            if (Thread.currentThread()
                .isInterrupted()) throw new IOException("已取消媒体请求");
            if (System.nanoTime() > deadline) throw new IOException("媒体传输超过 60 秒，请稍后重试");
            total += count;
            if (total > maximum) throw new IOException("音频或解析响应超过允许大小");
            output.write(buffer, 0, count);
        }
        if (total == 0) throw new IOException("平台返回空内容");
    }

    public static boolean allowedHost(String host, String provider) {
        if (host == null) return false;
        host = host.toLowerCase(Locale.ROOT);
        return provider.equals("netease")
            ? host.equals("music.163.com") || host.equals("y.music.163.com")
                || host.equals("163cn.tv")
                || host.endsWith(".music.126.net")
            : host.equals("u.y.qq.com") || host.equals("y.qq.com")
                || host.equals("c6.y.qq.com")
                || host.equals("aqqmusic.tc.qq.com")
                || host.endsWith(".stream.qqmusic.qq.com");
    }

    private static HttpURLConnection open(String address, String provider) throws IOException {
        for (int redirect = 0; redirect < 5; redirect++) {
            // Provider CDN links may still be HTTP; require HTTPS for the actual request.
            if (address.startsWith("http://")) address = "https://" + address.substring(7);
            final URI uri;
            try {
                uri = URI.create(address);
            } catch (IllegalArgumentException exception) {
                throw new IOException("无效媒体地址", exception);
            }
            if (!"https".equalsIgnoreCase(uri.getScheme()) || uri.getUserInfo() != null
                || uri.getPort() != -1
                || !allowedHost(uri.getHost(), provider)) throw new IOException("平台返回了不允许的媒体地址");
            for (InetAddress ip : InetAddress.getAllByName(uri.getHost()))
                if (ip.isAnyLocalAddress() || ip.isLoopbackAddress()
                    || ip.isLinkLocalAddress()
                    || ip.isSiteLocalAddress()
                    || ip.isMulticastAddress()
                    || (ip.getAddress().length == 16 && (ip.getAddress()[0] & 0xfe) == 0xfc))
                    throw new IOException("拒绝内网媒体地址");
            HttpURLConnection connection = (HttpURLConnection) new URL(address).openConnection();
            connection.setConnectTimeout(10000);
            connection.setReadTimeout(15000);
            connection.setInstanceFollowRedirects(false);
            connection.setRequestProperty("User-Agent", "DarkGreyRPG/0.3.3.2");
            connection
                .setRequestProperty("Referer", provider.equals("qq") ? "https://y.qq.com/" : "https://music.163.com/");
            final int status;
            try {
                status = connection.getResponseCode();
            } catch (IOException exception) {
                connection.disconnect();
                throw exception;
            }
            if (status >= 300 && status < 400) {
                String next = connection.getHeaderField("Location");
                connection.disconnect();
                if (next == null) throw new IOException("媒体重定向缺少目标");
                address = uri.resolve(next)
                    .toString();
                continue;
            }
            if (status != 200) {
                connection.disconnect();
                throw new IOException("平台请求失败，HTTP " + status);
            }
            if (connection.getContentLengthLong() > MAX_BYTES) {
                connection.disconnect();
                throw new IOException("媒体超过 32 MiB 上限");
            }
            return connection;
        }
        throw new IOException("媒体重定向次数过多");
    }
}
