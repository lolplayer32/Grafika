using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System;

namespace GrafikaSzeminarium
{
    internal class Program
    {
        private static IWindow graphicWindow;
        private static GL Gl;
        private static RubiksCubeModel rubiksCube;
        private static uint program;

        private const string ModelMatrixVariableName = "uModel";
        private const string ViewMatrixVariableName = "uView";
        private const string ProjectionMatrixVariableName = "uProjection";

        private static readonly string VertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 vPos;
        layout (location = 1) in vec4 vCol;

        uniform mat4 uModel;
        uniform mat4 uView;
        uniform mat4 uProjection;

        out vec4 outCol;
        
        void main()
        {
            outCol = vCol;
            gl_Position = uProjection * uView * uModel * vec4(vPos.x, vPos.y, vPos.z, 1.0);
        }
        ";

        private static readonly string FragmentShaderSource = @"
        #version 330 core
        out vec4 FragColor;
        
        in vec4 outCol;

        void main()
        {
            FragColor = outCol;
        }
        ";

        static void Main(string[] args)
        {
            WindowOptions windowOptions = WindowOptions.Default;
            windowOptions.Title = "Rubik Kocka";
            windowOptions.Size = new Silk.NET.Maths.Vector2D<int>(800, 800);

            graphicWindow = Window.Create(windowOptions);

            graphicWindow.Load += GraphicWindow_Load;
            graphicWindow.Render += GraphicWindow_Render;
            graphicWindow.Closing += GraphicWindow_Closing;

            graphicWindow.Run();
        }

        private static void GraphicWindow_Closing()
        {
            rubiksCube.Dispose();
            Gl.DeleteProgram(program);
        }

        private static unsafe void GraphicWindow_Load()
        {
            Gl = graphicWindow.CreateOpenGL();
            Gl.ClearColor(0.85f, 0.85f, 0.85f, 1.0f);

            Gl.Enable(EnableCap.DepthTest);
            Gl.DepthFunc(DepthFunction.Lequal);

            // A takarásban lévő háromszögek eldobása (CullFace)[cite: 7]
            Gl.Enable(EnableCap.CullFace);
            Gl.CullFace(TriangleFace.Back);

            uint vshader = Gl.CreateShader(ShaderType.VertexShader);
            Gl.ShaderSource(vshader, VertexShaderSource);
            Gl.CompileShader(vshader);

            uint fshader = Gl.CreateShader(ShaderType.FragmentShader);
            Gl.ShaderSource(fshader, FragmentShaderSource);
            Gl.CompileShader(fshader);

            program = Gl.CreateProgram();
            Gl.AttachShader(program, vshader);
            Gl.AttachShader(program, fshader);
            Gl.LinkProgram(program);

            Gl.DetachShader(program, vshader);
            Gl.DetachShader(program, fshader);
            Gl.DeleteShader(vshader);
            Gl.DeleteShader(fshader);

            Gl.UseProgram(program);

            // Kamera és projekciós mátrix fix beállítása a Load fázisban! 
            // Direkt egy magasabb, eltolt nézőpontot (Érdekes szöget) határoztam meg.
            var cameraPosition = new Vector3D<float>(5.0f, 4.0f, 6.0f);
            var cameraTarget = new Vector3D<float>(0.0f, 0.0f, 0.0f);
            var cameraUp = new Vector3D<float>(0.0f, 1.0f, 0.0f);
            var viewMatrix = Matrix4X4.CreateLookAt(cameraPosition, cameraTarget, cameraUp);
            SetMatrix(viewMatrix, ViewMatrixVariableName);

            var projectionMatrix = Matrix4X4.CreatePerspectiveFieldOfView<float>((float)(Math.PI / 4), 1.0f, 0.1f, 100f);
            SetMatrix(projectionMatrix, ProjectionMatrixVariableName);

            rubiksCube = RubiksCubeModel.Create(Gl);
        }

        private static unsafe void GraphicWindow_Render(double deltaTime)
        {
            Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            Gl.UseProgram(program);

            // Egy kis extrával a kocka lassan és folyamatosan forog is az érdekesebb vizuális végeredményért.
            float time = (float)graphicWindow.Time;
            Matrix4X4<float> globalRotation = Matrix4X4.CreateRotationY(time * 0.5f) * Matrix4X4.CreateRotationX(time * 0.3f);

            // Kirajzoljuk mind a 27 kiskockát egyedi pozíciójuk és egyedi ColorBufferük (VAO) alapján.
            foreach (var part in rubiksCube.Parts)
            {
                var finalModelMatrix = part.ModelMatrix * globalRotation;
                SetMatrix(finalModelMatrix, ModelMatrixVariableName);

                Gl.BindVertexArray(part.Vao);
                Gl.DrawElements(PrimitiveType.Triangles, rubiksCube.IndexArrayLength, DrawElementsType.UnsignedInt, null);
            }
            Gl.BindVertexArray(0);
        }

        private static unsafe void SetMatrix(Matrix4X4<float> mx, string uniformName)
        {
            int location = Gl.GetUniformLocation(program, uniformName);
            if (location != -1)
            {
                Gl.UniformMatrix4(location, 1, false, (float*)&mx);
            }
        }
    }
}