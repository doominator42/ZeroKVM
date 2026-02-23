namespace ZeroKvm.Hid;

internal interface IHidReport
{
    static abstract int ReportLength { get; }

    static abstract bool IsBootDescriptor { get; }

    static abstract ReadOnlySpan<byte> Descriptor { get; }

    static virtual void SetReportId(Span<byte> descriptor, byte reportId)
    {
        descriptor[1] = reportId;
    }
}
