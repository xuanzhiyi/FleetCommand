BACKGROUND MUSIC SETUP
======================
1. Copy your music file here and name it:
       bgmusic.wav

2. In Visual Studio, the .csproj already has:
       <EmbeddedResource Include="Resources\bgmusic.wav" ... />
   so the WAV is automatically embedded on build.

3. The game loads it via:
       Assembly.GetExecutingAssembly()
           .GetManifestResourceStream("FleetCommand.Resources.bgmusic.wav")
   which calls PlayLooping() — loops seamlessly during gameplay.

4. If the file is missing the game runs silently (music is optional).
