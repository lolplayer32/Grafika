using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
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
        private static ImGuiController imGuiController;

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

        // UI & Fény változók
        private static System.Numerics.Vector3 lightColor = new System.Numerics.Vector3(1f, 1f, 1f);
        private static System.Numerics.Vector3 lightPosition = new System.Numerics.Vector3(3f, 3f, 5f);

        // Animáció
        private static float currentSliceAngle = 0.0f;
        private static float targetSliceAngle = 0.0f;

        // --- ÚJ SHADEREK PHONG ÁRNYALÁSSAL ---
        private static readonly string VertexShaderSource = @"
        #version 330 core
        layout (location = 0) in vec3 vPos;
        layout (location = 1) in vec4 vCol;
        layout (location = 2) in vec3 vNormal;

        uniform mat4 uModel;
        uniform mat4 uSliceRotation;
        uniform mat4 uView;
        uniform mat4 uProjection;
        uniform mat3 uNormal;

        out vec4 outCol;
        out vec3 outNormal;
        out vec3 outWorldPosition;
        
        void main()
        {
            outCol = vCol;
            outNormal = uNormal * vNormal;
            vec4 worldPos = uSliceRotation * uModel * vec4(vPos, 1.0);
            outWorldPosition = vec3(worldPos);
            gl_Position = uProjection * uView * worldPos;
        }
        ";

        private static readonly string FragmentShaderSource = @"
        #version 330 core
        out vec4 FragColor;
        
        uniform vec3 uLightColor;
        uniform vec3 uLightPos;
        uniform vec3 uViewPos;

        in vec4 outCol;
        in vec3 outNormal;
        in vec3 outWorldPosition;

        void main() { 
            float ambientStrength = 0.15;
            vec3 ambient = ambientStrength * uLightColor;

            vec3 norm = normalize(outNormal);
            vec3 lightDir = normalize(uLightPos - outWorldPosition);
            float diff = max(dot(norm, lightDir), 0.0);
            vec3 diffuse = diff * uLightColor * 0.7;

            float specularStrength = 0.5;
            vec3 viewDir = normalize(uViewPos - outWorldPosition);
            vec3 reflectDir = reflect(-lightDir, norm);
            float spec = pow(max(dot(viewDir, reflectDir), 0.0), 32.0);
            vec3 specular = specularStrength * spec * uLightColor;

            vec3 result = (ambient + diffuse + specular) * outCol.rgb;
            FragColor = vec4(result, outCol.a);
        }
        ";

        static void Main(string[] args)
        {
            WindowOptions windowOptions = WindowOptions.Default;
            windowOptions.Title = "Rubik Kocka - Phong & UI";
            windowOptions.Size = new Silk.NET.Maths.Vector2D<int>(1024, 768);

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
            Gl.ClearColor(0.2f, 0.2f, 0.2f, 1.0f);

            Gl.Enable(EnableCap.DepthTest);
            Gl.DepthFunc(DepthFunction.Lequal);
            Gl.Enable(EnableCap.CullFace);
            Gl.CullFace(TriangleFace.Back);

            IInputContext inputContext = graphicWindow.CreateInput();
            primaryKeyboard = inputContext.Keyboards.FirstOrDefault();

            // ImGui inicializálása
            imGuiController = new ImGuiController(Gl, graphicWindow, inputContext);

            uint vshader = Gl.CreateShader(ShaderType.VertexShader);
            Gl.ShaderSource(vshader, VertexShaderSource);
            Gl.CompileShader(vshader);
            Gl.GetShader(vshader, ShaderParameterName.CompileStatus, out int vStatus);
            if (vStatus != (int)GLEnum.True) throw new Exception("Vertex shader hiba: " + Gl.GetShaderInfoLog(vshader));

            uint fshader = Gl.CreateShader(ShaderType.FragmentShader);
            Gl.ShaderSource(fshader, FragmentShaderSource);
            Gl.CompileShader(fshader);
            Gl.GetShader(fshader, ShaderParameterName.CompileStatus, out int fStatus);
            if (fStatus != (int)GLEnum.True) throw new Exception("Fragment shader hiba: " + Gl.GetShaderInfoLog(fshader));

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
            var projectionMatrix = Matrix4X4.CreatePerspectiveFieldOfView<float>((float)(Math.PI / 4), 1024f / 768f, 0.1f, 100f);
            SetMatrix(projectionMatrix, ProjectionMatrixVariableName);
        }

        private static void GraphicWindow_Update(double deltaTime)
        {
            if (primaryKeyboard == null) return;

            // ImGui frissítése
            imGuiController.Update((float)deltaTime);

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

            // Kamera beállítása
            var viewMatrix = Matrix4X4.CreateLookAt(cameraPosition, cameraPosition + cameraFront, cameraUp);
            SetMatrix(viewMatrix, ViewMatrixVariableName);

            // Fény beállítása a UI paraméterekből
            SetUniform3("uLightColor", lightColor);
            SetUniform3("uLightPos", lightPosition);
            SetUniform3("uViewPos", new System.Numerics.Vector3(cameraPosition.X, cameraPosition.Y, cameraPosition.Z));

            foreach (var part in rubiksCube.Parts)
            {
                SetMatrix(part.ModelMatrix, ModelMatrixVariableName);

                var sliceRot = Matrix4X4<float>.Identity;
                if (part.GridY == 1) // Forgó réteg beállítása
                {
                    sliceRot = Matrix4X4.CreateRotationY(currentSliceAngle * (float)Math.PI / 180f);
                }
                SetMatrix(sliceRot, SliceRotationVariableName);

                // --- NORMÁLMÁTRIX SZÁMÍTÁSA PHONG-HOZ ---
                var finalModelMatrix = part.ModelMatrix * sliceRot;
                var modelMatrixWithoutTranslation = new Matrix4X4<float>(finalModelMatrix.Row1, finalModelMatrix.Row2, finalModelMatrix.Row3, finalModelMatrix.Row4);
                modelMatrixWithoutTranslation.M41 = 0; modelMatrixWithoutTranslation.M42 = 0; modelMatrixWithoutTranslation.M43 = 0; modelMatrixWithoutTranslation.M44 = 1;

                Matrix4X4.Invert<float>(modelMatrixWithoutTranslation, out var modelInvers);
                Matrix3X3<float> normalMatrix = new Matrix3X3<float>(Matrix4X4.Transpose(modelInvers));

                int normalLoc = Gl.GetUniformLocation(program, "uNormal");
                if (normalLoc != -1) Gl.UniformMatrix3(normalLoc, 1, false, (float*)&normalMatrix);

                Gl.BindVertexArray(part.Vao);
                Gl.DrawElements(PrimitiveType.Triangles, rubiksCube.IndexArrayLength, DrawElementsType.UnsignedInt, null);
            }
            Gl.BindVertexArray(0);

            // --- IMGUI UI MEGRAJZOLÁSA ---
            ImGuiNET.ImGui.Begin("Rubik Kocka Vezerlo", ImGuiNET.ImGuiWindowFlags.AlwaysAutoResize | ImGuiNET.ImGuiWindowFlags.NoCollapse);

            // Fény paraméterek (Csúszkák + Szövegmezők)
            ImGuiNET.ImGui.SliderFloat3("Feny Szine (RGB)", ref lightColor, 0f, 1f);
            ImGuiNET.ImGui.InputFloat3("Feny Pozicioja", ref lightPosition);

            ImGuiNET.ImGui.Separator();
            ImGuiNET.ImGui.Text("Navigacio (Forgatasok):");

            // Forgatást vezérlő gombok
            if (ImGuiNET.ImGui.Button("Felso reteg balra (-90f)"))
            {
                targetSliceAngle -= 90f;
            }
            ImGuiNET.ImGui.SameLine();
            if (ImGuiNET.ImGui.Button("Felso reteg jobbra (+90f)"))
            {
                targetSliceAngle += 90f;
            }

            ImGuiNET.ImGui.End();

            // ImGui kirenderelése
            imGuiController.Render();
        }

        private static unsafe void SetMatrix(Matrix4X4<float> mx, string uniformName)
        {
            int location = Gl.GetUniformLocation(program, uniformName);
            if (location != -1)
            {
                Gl.UniformMatrix4(location, 1, false, (float*)&mx);
            }
        }

        private static unsafe void SetUniform3(string uniformName, System.Numerics.Vector3 uniformValue)
        {
            int location = Gl.GetUniformLocation(program, uniformName);
            if (location != -1)
            {
                Gl.Uniform3(location, uniformValue);
            }
        }
    }
}