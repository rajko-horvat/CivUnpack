## DOS Civilization (1991) EXE Unpacker
<p>Unpacks DOS version of Civilization (1991) (all versions work).</p>
<p>Download the latest version from <a href="https://github.com/rajko-horvat/CivUnpack/releases" target="_blank">Releases page</a>. 
Extract the zip archive into directory with CIV.EXE and run CivUnpack.</p>
<p>The backup will be created as CIV.bak and unpacked version will be written as CIV.EXE
The old CIV.bak is not needed, as all EXE overlays are preserved.</p>

## Dependencies
<ul>
<li>.NET Core 8</li>
</ul>

## How to compile the code (.NET Core 8 SDK required)
<p>You can compile the code with Visual Studio 2022.</p>
Or, you can also compile with CLI method:
<ul>
<li>git clone https://github.com/rajko-horvat/CivUnpack</li>
<li>cd CivUnpack</li>
<li>dotnet build -c Debug</li>
</ul>