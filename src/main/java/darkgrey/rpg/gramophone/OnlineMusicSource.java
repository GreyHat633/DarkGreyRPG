package darkgrey.rpg.gramophone;

import java.net.URI;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/** Persist only the provider identity, never an expiring signed media URL. */
public final class OnlineMusicSource {

    public final String provider;
    public final String key;

    private OnlineMusicSource(String provider, String key) {
        this.provider = provider;
        this.key = key;
    }

    public static OnlineMusicSource parse(String text) {
        if (text == null || text.length() > 2048) throw new IllegalArgumentException("歌曲链接过长或为空");
        try {
            URI uri = new URI(text.trim());
            if (!"https".equalsIgnoreCase(uri.getScheme()) || uri.getUserInfo() != null || uri.getPort() != -1)
                throw new IllegalArgumentException("请使用 QQ 音乐或网易云的 HTTPS 单曲链接");
            String host = uri.getHost();
            if ("music.163.com".equalsIgnoreCase(host) || "y.music.163.com".equalsIgnoreCase(host)) {
                Matcher id = Pattern.compile("(?:[?&])id=([0-9]{1,20})(?:&|$)")
                    .matcher(uri.toString());
                if (uri.toString()
                    .contains("/song") && id.find()) return new OnlineMusicSource("netease", id.group(1));
            }
            if ("y.qq.com".equalsIgnoreCase(host)) {
                Matcher mid = Pattern.compile("/songDetail/([A-Za-z0-9]{14})(?:[/?#]|$)")
                    .matcher(uri.toString());
                if (mid.find()) return new OnlineMusicSource("qq", mid.group(1));
                mid = Pattern.compile("[?&]songmid=([A-Za-z0-9]{14})(?:&|#|$)")
                    .matcher(uri.toString());
                if (mid.find()) return new OnlineMusicSource("qq", mid.group(1));
            }
        } catch (java.net.URISyntaxException exception) {
            throw new IllegalArgumentException("歌曲链接格式无效");
        }
        throw new IllegalArgumentException("尚不能识别此单曲链接，请复制歌曲详情页 HTTPS 链接");
    }

    public String canonical() {
        return provider.equals("qq") ? "https://y.qq.com/n/ryqq/songDetail/" + key
            : "https://music.163.com/song?id=" + key;
    }
}
