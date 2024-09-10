using System;

public class Entity
{
    private IntPtr _baseAddress;
    private MemoryManager _memoryManager;

    public Entity(IntPtr baseAddress, MemoryManager memoryManager)
    {
        _baseAddress = baseAddress;
        _memoryManager = memoryManager;
    }

    public int Health => _memoryManager.ReadInt32(_baseAddress + Offsets.m_iHealth);
    public int Team => _memoryManager.ReadInt32(_baseAddress + Offsets.m_iTeamNum);
    public float Speed => _memoryManager.ReadFloat(_baseAddress + Offsets.m_flSpeed);

    public (float X, float Y, float Z) Position
    {
        get
        {
            float x = _memoryManager.ReadFloat(_baseAddress + Offsets.m_vecOrigin);
            float y = _memoryManager.ReadFloat(_baseAddress + Offsets.m_vecOrigin + 0x4);
            float z = _memoryManager.ReadFloat(_baseAddress + Offsets.m_vecOrigin + 0x8);
            return (x, y, z);
        }
    }

    public (float X, float Y, float Z) Velocity
    {
        get
        {
            float x = _memoryManager.ReadFloat(_baseAddress + Offsets.m_vecVelocity);
            float y = _memoryManager.ReadFloat(_baseAddress + Offsets.m_vecVelocity + 0x4);
            float z = _memoryManager.ReadFloat(_baseAddress + Offsets.m_vecVelocity + 0x8);
            return (x, y, z);
        }
    }

    public float Gravity => _memoryManager.ReadFloat(_baseAddress + Offsets.m_flGravityScale);
    public float Friction => _memoryManager.ReadFloat(_baseAddress + Offsets.m_flFriction);
    public float Elasticity => _memoryManager.ReadFloat(_baseAddress + Offsets.m_flElasticity);
}