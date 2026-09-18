JLayer 1.0.1
===========
Artifact: javazoom:jlayer:1.0.1 (Maven Central)
Binary SHA-256: 850508C837454A1B06017C32A36876FAE516DE1E89A829F725FEE1E6DCC52000
License: GNU Lesser General Public License 2.1; see LICENSE.txt.
Corresponding upstream source is included as jlayer-1.0.1-sources.jar.
The library classes are included unchanged under javazoom/jl in the mod JAR.
DarkGreyRPG's CodecGramophoneMp3 is a separate adapter; no JLayer source is modified.

Rebuilding/replacing the library: replace libs/jlayer-1.0.1.jar with a compatible
JLayer build, then run gradlew build. The jar task embeds those unrelocated classes.
For an existing binary mod, the unrelocated javazoom/jl entries may likewise be
replaced with a compatible library build; preserve the mod's other entries.
The source archive and this notice are bundled in META-INF/licenses/jlayer.
