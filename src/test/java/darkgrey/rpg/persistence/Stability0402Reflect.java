package darkgrey.rpg.persistence;

import java.lang.reflect.Field;
import java.lang.reflect.Method;

/** Test-only access to both development and formal Forge names. */
public final class Stability0402Reflect {

    private Stability0402Reflect() {}

    public static Object call(Object target, String mcp, String srg) throws Exception {
        return method(target.getClass(), mcp, srg).invoke(target);
    }

    public static Method method(Class<?> type, String mcp, String srg, Class<?>... parameters) throws Exception {
        try {
            return type.getMethod(mcp, parameters);
        } catch (NoSuchMethodException missing) {
            return type.getMethod(srg, parameters);
        }
    }

    public static Object read(Object target, String mcp, String srg) throws Exception {
        for (Class<?> type = target.getClass(); type != null; type = type.getSuperclass()) {
            for (String name : new String[] { mcp, srg }) try {
                Field field = type.getDeclaredField(name);
                field.setAccessible(true);
                return field.get(target);
            } catch (NoSuchFieldException missing) {}
        }
        throw new NoSuchFieldException(mcp + "/" + srg);
    }
}
