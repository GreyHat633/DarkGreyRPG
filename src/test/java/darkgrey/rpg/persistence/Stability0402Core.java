package darkgrey.rpg.persistence;

import java.util.Map;

import cpw.mods.fml.relauncher.IFMLLoadingPlugin;

/** Test-only tick sampler. */
@IFMLLoadingPlugin.MCVersion("1.7.10")
@IFMLLoadingPlugin.SortingIndex(1001)
@IFMLLoadingPlugin.TransformerExclusions({ "darkgrey.rpg.persistence.Stability0402" })
public final class Stability0402Core implements IFMLLoadingPlugin {

    public String[] getASMTransformerClass() {
        return new String[] { "darkgrey.rpg.persistence.Stability0402Transformer" };
    }

    public String getModContainerClass() {
        return null;
    }

    public String getSetupClass() {
        return null;
    }

    public void injectData(Map<String, Object> data) {}

    public String getAccessTransformerClass() {
        return null;
    }
}
