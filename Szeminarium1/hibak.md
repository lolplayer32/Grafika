1. Labor - OpenGL Hibakereses tapasztalatok

1. BindBuffer kihagyasa
147.sor
Mit csinaltam: Kikommenteztem a Gl.BindBuffer(GLEnum.ArrayBuffer, vertices); sort a Vertex Buffer beallitasanal.
Eredmeny: Egy InvalidOperation (1282) hibat dobott a konzolra. A haromszog nem rajzolodott ki, mert a BufferData fuggveny nem tudta hova masolni az adatokat a memoriaban.

2. VertexAttribArray tulindexelese
155. sor
Mit csinaltam: A Gl.EnableVertexAttribArray(1) parameteret atirtam 999-re.
Eredmeny: Az OpenGL InvalidValue (1281) hibat jelzett, mivel a GPU-n nincs ennyi elerheto attributum hely, tulkeptunk a megengedett tartomanyon.

3. Shader szintaktikai hiba
17. sor
Mit csinaltam: Kitoroltem egy pontosvesszot a Vertex Shader forraskodjabol (VertexShaderSource).
Eredmeny: A shader fordito hibat dobott. A program egy Exception kiserleteben leallt, es kiirta a konzolra a pontos szintaktikai hibat es a hiba helyet.

4. Tomb elrontasa
99.sor
Mit csinaltam: Kevesebb elemet adtam meg a vertexArray tombben.
Eredmeny: Ez nem OpenGL, hanem C# nyelvi hibat eredmenyezett. A program IndexOutOfRangeException hibat dobott.