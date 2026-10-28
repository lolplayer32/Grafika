using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.Windowing;
using System;
using System.Linq;

namespace GrafikaSzeminarium
{
    internal class Program
    {
        private static IWindow graphicWindow;
        private static GL Gl;
        private static IKeyboard primaryKeyboard;
        private static RubiksCubeModel rubiksCube;
        private static uint program;

        private const string ModelMatrixVariableName = "uModel";
        private const string SliceRotationVariableName = "uSliceRotation";
        private const string ViewMatrixVariableName = "uView";
        private const string ProjectionMatrixVariableName = "uProjection";

        // Kamera
        private static Vector3D<float> cameraPosition = new Vector3D<float>(0.0f, 2.0f, 8.0f);
        private static Vector3D<float> cameraFront = new Vector3D<float>(0.0f, 0.0f, -1.0f);
        private static Vector3D<float> cameraUp = new Vector3D<float>(0.0f, 1.0f, 0.0f);
        private static float cameraYaw = -90.0f;
        private static float cameraPitch = 0.0f;

        // Animáció
        private static float currentSliceAngle = 0.0f;
        private static float targetSliceAngle = 0.0f;

        private static readonly string VertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 vPos;
        layout (location = 1) in vec4 vCol;

        uniform mat4 uModel;
        uniform mat4 uSliceRotation;
        uniform mat4 uView;
        uniform mat4 uProjection;

        out vec4 outCol;
        
        void main()
        {
            outCol = vCol;
            gl_Position = uProjection * uView * uSliceRotation * uModel * vec4(vPos.x, vPos.y, vPos.z, 1.0);
        }
        ";

        private static readonly string FragmentShaderSource = @"
        #version 330 core
        out vec4 FragColor;
        in vec4 outCol;
        void main() { FragColor = outCol; }
        ";

        static void Main(string[] args)
        {
            WindowOptions windowOptions = WindowOptions.Default;
            windowOptions.Title = "Rubik Kocka - Kamera es Animacio";
            windowOptions.Size = new Silk.NET.Maths.Vector2D<int>(800, 800);

            graphicWindow = Window.Create(windowOptions);
            graphicWindow.Load += GraphicWindow_Load;
            graphicWindow.Update += GraphicWindow_Update;
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
            Gl.Enable(EnableCap.CullFace);
            Gl.CullFace(TriangleFace.Back);

            IInputContext inputContext = graphicWindow.CreateInput();
            primaryKeyboard = inputContext.Keyboards.FirstOrDefault();
            if (primaryKeyboard != null)
            {
                primaryKeyboard.KeyDown += KeyDown;
            }

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

            rubiksCube = RubiksCubeModel.Create(Gl);

            Gl.UseProgram(program);
            var projectionMatrix = Matrix4X4.CreatePerspectiveFieldOfView<float>((float)(Math.PI / 4), 1.0f, 0.1f, 100f);
            SetMatrix(projectionMatrix, ProjectionMatrixVariableName);
        }

        private static void KeyDown(IKeyboard keyboard, Key key, int arg3)
        {
            if (key == Key.Space) targetSliceAngle += 90f;
            if (key == Key.Backspace) targetSliceAngle -= 90f;
        }

        private static void GraphicWindow_Update(double deltaTime)
        {
            if (primaryKeyboard == null) return;

            // Kamera navigáció (Mozgás)
            float moveSpeed = 5.0f * (float)deltaTime;
            if (primaryKeyboard.IsKeyPressed(Key.Up)) cameraPosition += cameraFront * moveSpeed;
            if (primaryKeyboard.IsKeyPressed(Key.Down)) cameraPosition -= cameraFront * moveSpeed;
            if (primaryKeyboard.IsKeyPressed(Key.Left)) cameraPosition -= Vector3D.Normalize(Vector3D.Cross(cameraFront, cameraUp)) * moveSpeed;
            if (primaryKeyboard.IsKeyPressed(Key.Right)) cameraPosition += Vector3D.Normalize(Vector3D.Cross(cameraFront, cameraUp)) * moveSpeed;

            // Kamera navigáció (Nézelődés)
            float rotSpeed = 90.0f * (float)deltaTime;
            if (primaryKeyboard.IsKeyPressed(Key.A)) cameraYaw -= rotSpeed;
            if (primaryKeyboard.IsKeyPressed(Key.D)) cameraYaw += rotSpeed;
            if (primaryKeyboard.IsKeyPressed(Key.W)) cameraPitch += rotSpeed;
            if (primaryKeyboard.IsKeyPressed(Key.S)) cameraPitch -= rotSpeed;

            cameraPitch = Math.Clamp(cameraPitch, -89.0f, 89.0f);

            Vector3D<float> front;
            front.X = (float)(Math.Cos(cameraYaw * Math.PI / 180.0) * Math.Cos(cameraPitch * Math.PI / 180.0));
            front.Y = (float)(Math.Sin(cameraPitch * Math.PI / 180.0));
            front.Z = (float)(Math.Sin(cameraYaw * Math.PI / 180.0) * Math.Cos(cameraPitch * Math.PI / 180.0));
            cameraFront = Vector3D.Normalize(front);

            // Forgatási animáció frissítése
            if (currentSliceAngle != targetSliceAngle)
            {
                float animSpeed = 180f * (float)deltaTime;
                if (currentSliceAngle < targetSliceAngle)
                {
                    currentSliceAngle += animSpeed;
                    if (currentSliceAngle > targetSliceAngle) currentSliceAngle = targetSliceAngle;
                }
                else
                {
                    currentSliceAngle -= animSpeed;
                    if (currentSliceAngle < targetSliceAngle) currentSliceAngle = targetSliceAngle;
                }
            }
        }

        private static unsafe void GraphicWindow_Render(double deltaTime)
        {
            Gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            Gl.UseProgram(program);

            var viewMatrix = Matrix4X4.CreateLookAt(cameraPosition, cameraPosition + cameraFront, cameraUp);
            SetMatrix(viewMatrix, ViewMatrixVariableName);

            foreach (var part in rubiksCube.Parts)
            {
                SetMatrix(part.ModelMatrix, ModelMatrixVariableName);

                if (part.GridY == 1)
                {
                    var sliceRot = Matrix4X4.CreateRotationY(currentSliceAngle * (float)Math.PI / 180f);
                    SetMatrix(sliceRot, SliceRotationVariableName);
                }
                else
                {
                    SetMatrix(Matrix4X4<float>.Identity, SliceRotationVariableName);
                }

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