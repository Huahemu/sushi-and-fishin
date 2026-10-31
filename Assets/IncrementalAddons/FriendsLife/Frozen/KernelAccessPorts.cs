namespace TomodachiLife.Simulation
{
    // New access ports only. All decisions and arithmetic stay in the copied source.
    public sealed partial class LifeSimulationRuntime
    {
        public string KernelFamilyActionReason(string focusId, string targetId, string action)
            => FamilyActionReason(focusId, targetId, action);

        public bool KernelDailyActionMatches(LifeDailyRequest request, string detail, string targetId, string category)
            => DailyActionMatches(request, detail, targetId, category);

        public void KernelEndDailyRequest(LifeDailyRequest request, bool success, string message)
            => EndDailyRequest(request, success, message);
    }
}
