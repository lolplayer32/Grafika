using Silk.NET.Maths;
using Silk.NET.OpenGL;
using System;
using System.Collections.Generic;

namespace GrafikaSzeminarium
{
    internal class RubiksCubeModel : IDisposable
    {
        private bool disposedValue;

        public uint Vertices { get; private set; }
        public uint Indices { get; private set; }
        public uint IndexArrayLength { get; private set; }

        public List<CubePart> Parts { get; private set; } = new List<CubePart>();

        private GL Gl;

        public struct CubePart
        {
            public uint Vao;
            public uint Colors;
            public Matrix4X4<float> ModelMatrix;
        }

        public unsafe static RubiksCubeModel Create(GL Gl)
        {
            var model = new RubiksCubeModel { Gl = Gl };

            // Csúcsok (Ugyanaz mind a 27 kockára)
            var vertexArray = new float[] {
                // Felső (Top)
                -0.5f, 0.5f, 0.5f,   0.5f, 0.5f, 0.5f,   0.5f, 0.5f, -0.5f,  -0.5f, 0.5f, -0.5f,
                // Elülső (Front)
                -0.5f, 0.5f, 0.5f,  -0.5f, -0.5f, 0.5f,  0.5f, -0.5f, 0.5f,   0.5f, 0.5f, 0.5f,
                // Bal (Left)
                -0.5f, 0.5f, 0.5f,  -0.5f, 0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.5f, 0.5f,
                // Alsó (Bottom)
                -0.5f, -0.5f, 0.5f,  0.5f, -0.5f, 0.5f,  0.5f, -0.5f, -0.5f, -0.5f, -0.5f, -0.5f,
                // Hátsó (Back)
                 0.5f, 0.5f, -0.5f, -0.5f, 0.5f, -0.5f, -0.5f, -0.5f, -0.5f,  0.5f, -0.5f, -0.5f,
                // Jobb (Right)
                 0.5f, 0.5f, 0.5f,   0.5f, 0.5f, -0.5f,  0.5f, -0.5f, -0.5f,  0.5f, -0.5f, 0.5f,
            };

            uint[] indexArray = new uint[] {
                0, 1, 2,  0, 2, 3,       // Felső
                4, 5, 6,  4, 6, 7,       // Elülső
                8, 9, 10,  8, 10, 11,    // Bal
                12, 14, 13,  12, 15, 14, // Alsó
                17, 16, 19,  17, 19, 18, // Hátsó
                20, 22, 21,  20, 23, 22  // Jobb
            };

            model.IndexArrayLength = (uint)indexArray.Length;

            // 1. Közös Vertex Buffer (Csúcskoordináták) beküldése egyszer!
            model.Vertices = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ArrayBuffer, model.Vertices);
            Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)vertexArray.AsSpan(), GLEnum.StaticDraw);

            // 2. Közös Index Buffer
            model.Indices = Gl.GenBuffer();
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, model.Indices);
            Gl.BufferData(GLEnum.ElementArrayBuffer, (ReadOnlySpan<uint>)indexArray.AsSpan(), GLEnum.StaticDraw);

            Gl.BindBuffer(GLEnum.ArrayBuffer, 0);
            Gl.BindBuffer(GLEnum.ElementArrayBuffer, 0);

            float gap = 1.05f; // Rés a kis kockák között
            float[] darkGray = new float[] { 0.15f, 0.15f, 0.15f, 1.0f }; // Belső lapok színe

            // 27 kocka (VAO) létrehozása
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    for (int z = -1; z <= 1; z++)
                    {
                        var part = new CubePart();
                        part.ModelMatrix = Matrix4X4.CreateTranslation(x * gap, y * gap, z * gap);

                        float[] colorArray = new float[24 * 4];

                        // Színek meghatározása: ha a kocka kint van a felületen, megkapja a színt, különben belső és szürke lesz.
                        SetFaceColor(colorArray, 0, y == 1 ? new float[] { 1, 1, 1, 1 } : darkGray);     // Felső = Fehér
                        SetFaceColor(colorArray, 1, z == 1 ? new float[] { 0, 0.7f, 0, 1 } : darkGray);  // Elülső = Zöld
                        SetFaceColor(colorArray, 2, x == -1 ? new float[] { 1, 0.5f, 0, 1 } : darkGray); // Bal = Narancs
                        SetFaceColor(colorArray, 3, y == -1 ? new float[] { 1, 1, 0, 1 } : darkGray);    // Alsó = Sárga
                        SetFaceColor(colorArray, 4, z == -1 ? new float[] { 0, 0, 1, 1 } : darkGray);    // Hátsó = Kék
                        SetFaceColor(colorArray, 5, x == 1 ? new float[] { 1, 0, 0, 1 } : darkGray);     // Jobb = Piros

                        // Saját, egyedi VAO generálása
                        part.Vao = Gl.GenVertexArray();
                        Gl.BindVertexArray(part.Vao);

                        // Megosztott csúcsok bekötése a helyi VAO-hoz
                        Gl.BindBuffer(GLEnum.ArrayBuffer, model.Vertices);
                        Gl.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, null);
                        Gl.EnableVertexAttribArray(0);

                        // EGYEDI színek Bufferének bekötése
                        part.Colors = Gl.GenBuffer();
                        Gl.BindBuffer(GLEnum.ArrayBuffer, part.Colors);
                        Gl.BufferData(GLEnum.ArrayBuffer, (ReadOnlySpan<float>)colorArray.AsSpan(), GLEnum.StaticDraw);
                        Gl.VertexAttribPointer(1, 4, VertexAttribPointerType.Float, false, 0, null);
                        Gl.EnableVertexAttribArray(1);

                        // Megosztott Index Buffer bekötése a VAO állapothoz
                        Gl.BindBuffer(GLEnum.ElementArrayBuffer, model.Indices);

                        Gl.BindVertexArray(0);
                        model.Parts.Add(part);
                    }
                }
            }

            return model;
        }

        private static void SetFaceColor(float[] colorArray, int faceIndex, float[] color)
        {
            int offset = faceIndex * 4 * 4;
            for (int i = 0; i < 4; i++)
            {
                colorArray[offset + i * 4 + 0] = color[0];
                colorArray[offset + i * 4 + 1] = color[1];
                colorArray[offset + i * 4 + 2] = color[2];
                colorArray[offset + i * 4 + 3] = color[3];
            }
        }

        public void Dispose()
        {
            if (!disposedValue)
            {
                foreach (var part in Parts)
                {
                    Gl.DeleteBuffer(part.Colors);
                    Gl.DeleteVertexArray(part.Vao);
                }
                Gl.DeleteBuffer(Vertices);
                Gl.DeleteBuffer(Indices);

                disposedValue = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}