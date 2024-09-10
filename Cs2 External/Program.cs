using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;

class Program
{
    [DllImport("user32.dll")]
    static extern short GetAsyncKeyState(int vKey);

    static ConcurrentDictionary<int, Entity> entities = new ConcurrentDictionary<int, Entity>();
    static readonly object aimAngleLock = new object();

    static void Main(string[] args)
    {
        Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;

        string processName = "cs2";
        string moduleName = "client.dll";

        Process[] processes = Process.GetProcessesByName(processName);
        if (processes.Length == 0)
        {
            Console.WriteLine("Jogo não encontrado.");
            return;
        }

        Process process = processes[0];
        IntPtr moduleBase = IntPtr.Zero;

        foreach (ProcessModule module in process.Modules)
        {
            if (module.ModuleName == moduleName)
            {
                moduleBase = module.BaseAddress;
                break;
            }
        }

        if (moduleBase == IntPtr.Zero)
        {
            Console.WriteLine("Módulo não encontrado.");
            return;
        }

        MemoryManager memoryManager = new MemoryManager(process);
        IntPtr entityListBase = moduleBase + Offsets.EntityList;

        // Start the reading thread
        Task.Run(() => ReadEntities(memoryManager, entityListBase));

        // Get the key or button to activate the aimbot
        int triggerKey = GetFirstKeyPressed();

        // Main loop for aiming and writing angles
        while (true)
        {
            if ((GetAsyncKeyState(triggerKey) & 0x8000) != 0)  // Check if the trigger key is pressed
            {
                IntPtr localPlayerAddr = memoryManager.ReadIntPtr(moduleBase + Offsets.LocalPlayer);
                Entity localPlayer = new Entity(localPlayerAddr, memoryManager);

                AutoAim(localPlayer, memoryManager, moduleBase);
            }

            Thread.Sleep(0);  // High frequency update
        }
    }

    static int GetFirstKeyPressed()
    {
        Console.WriteLine("Pressione qualquer tecla ou botão do mouse para definir como gatilho do aimbot...");
        int[] keyCodes = Enumerable.Range(1, 190).ToArray();  // Reduzido o range para teclas válidas

        while (true)
        {
            foreach (int code in keyCodes)
            {
                if ((GetAsyncKeyState(code) & 0x8000) != 0)
                {
                    while ((GetAsyncKeyState(code) & 0x8000) != 0) { }  // Espera soltar a tecla
                    Console.WriteLine($"Tecla pressionada: {code}");
                    return code;
                }
            }
            Thread.Sleep(10);  // Evita uso excessivo de CPU
        }
    }

    static void ReadEntities(MemoryManager memoryManager, IntPtr entityListBase)
    {
        int entityCount = 64;  // Suponha que o número máximo de entidades seja 64
        int currentOffset = 0;

        while (true)
        {
            for (int i = 0; i < entityCount; i++)
            {
                // Ler entidade com base no offset atual
                IntPtr entityAddress = memoryManager.ReadIntPtr(entityListBase + currentOffset);

                if (entityAddress != IntPtr.Zero)
                {
                    Entity entity = new Entity(entityAddress, memoryManager);
                    entities[i] = entity;
                    currentOffset += Offsets.EntityListDelta;  // Segue o incremento padrão de 0x10
                }
                else
                {
                    // Se a entidade for nula, ajustar o próximo incremento para 0x20
                    currentOffset += 0x10;
                }
            }

            Thread.Sleep(10);  // Espera 10 ms antes da próxima leitura
            currentOffset = 0;  // Reinicia o offset para a próxima iteração
        }
    }


    static void AutoAim(Entity localPlayer, MemoryManager memoryManager, IntPtr clientDllBase)
    {
        Entity closestEntity = null;
        float closestDist = float.MaxValue;  // Inicia com uma distância máxima

        (float X, float Y, float Z) localPlayerPos = localPlayer.Position;

        IntPtr viewAnglesAddr = clientDllBase + Offsets.dwViewAngles;

        float currentPitch = memoryManager.ReadFloat(viewAnglesAddr);
        float currentYaw = memoryManager.ReadFloat(viewAnglesAddr + 0x4);

        float aimFOV = 30.0f;  // Defina o FOV desejado (em graus)

        foreach (var entity in entities.Values)
        {
            if (entity.Health > 0 && entity.Health <= 100 && entity.Team != localPlayer.Team)
            {
                float dist = CalculateDistance(localPlayerPos, entity.Position);

                var targetPosition = entity.Position;
                var targetAngle = CalculateAngle(localPlayerPos, targetPosition, (currentPitch, currentYaw));

                float angleDifference = CalculateAngleDifference((currentPitch, currentYaw), targetAngle);

                // Verificar se a entidade está dentro do FOV e se a distância é menor que a menor distância já encontrada
                if (angleDifference < aimFOV && dist < closestDist)
                {
                    closestDist = dist;  // Atualiza a menor distância encontrada
                    closestEntity = entity;  // Define essa entidade como o alvo mais próximo
                }
            }
        }

        if (closestEntity != null)
        {
            var targetPosition = closestEntity.Position;

            // Compensação dinâmica baseada na distância
            float distanceCompensation = closestDist / 200f;  // Ajuste o divisor conforme necessário
            targetPosition.Z -= 3f + distanceCompensation;  // Ajuste para mirar mais baixo

            var targetAngle = CalculateAngle(localPlayerPos, targetPosition, (currentPitch, currentYaw));

            // Ajuste o fator de suavização conforme necessário
            float smoothingFactor = 10f;  // Aumente este valor para uma transição mais suave

            var smoothAngle = SmoothAim((currentPitch, currentYaw), targetAngle, smoothingFactor);

            lock (aimAngleLock)
            {
                memoryManager.WriteFloat(viewAnglesAddr, smoothAngle.Pitch);
                memoryManager.WriteFloat(viewAnglesAddr + 0x4, smoothAngle.Yaw);
            }

            Console.WriteLine($"Aim Angle: Pitch = {smoothAngle.Pitch}, Yaw = {smoothAngle.Yaw}");
        }
    }

    static float CalculateAngleDifference((float Pitch, float Yaw) currentAngles, (float Pitch, float Yaw) targetAngles)
    {
        float deltaPitch = Math.Abs(currentAngles.Pitch - targetAngles.Pitch);
        float deltaYaw = Math.Abs(currentAngles.Yaw - targetAngles.Yaw);

        // Ajusta o deltaYaw para estar no intervalo [0, 180]
        if (deltaYaw > 180)
        {
            deltaYaw = 360 - deltaYaw;
        }

        return (float)Math.Sqrt(deltaPitch * deltaPitch + deltaYaw * deltaYaw);
    }

    static float CalculateDistance((float X, float Y, float Z) pos1, (float X, float Y, float Z) pos2)
    {
        return (float)Math.Sqrt(
            Math.Pow(pos2.X - pos1.X, 2) +
            Math.Pow(pos2.Y - pos1.Y, 2) +
            Math.Pow(pos2.Z - pos1.Z, 2)
        );
    }

    static (float Pitch, float Yaw) CalculateAngle((float X, float Y, float Z) source, (float X, float Y, float Z) target, (float Pitch, float Yaw) currentAngles)
    {
        float deltaX = target.X - source.X;
        float deltaY = target.Y - source.Y;
        float deltaZ = target.Z - source.Z;

        float hyp = (float)Math.Sqrt(deltaX * deltaX + deltaY * deltaY);

        float pitch = (float)Math.Atan2(-deltaZ, hyp) * (180 / (float)Math.PI);
        float yaw = (float)Math.Atan2(deltaY, deltaX) * (180 / (float)Math.PI);

        return (pitch, yaw);
    }

    static (float Pitch, float Yaw) SmoothAim((float Pitch, float Yaw) currentAngles, (float Pitch, float Yaw) targetAngles, float smoothingFactor)
    {
        float smoothPitch = currentAngles.Pitch + (targetAngles.Pitch - currentAngles.Pitch) / smoothingFactor;
        float smoothYaw = currentAngles.Yaw + (targetAngles.Yaw - currentAngles.Yaw) / smoothingFactor;
        return (smoothPitch, smoothYaw);
    }
}
