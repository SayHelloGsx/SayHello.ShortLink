using System;

namespace SayHello.ShortLink.Common.ShortLinks;

public interface IVisitorHashService
{
    string Compute(string? ipAddress, DateTime visitedAt);
}
