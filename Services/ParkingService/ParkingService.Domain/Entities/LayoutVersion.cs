using ParkingManagement.SharedKernel.Domain;

namespace ParkingService.Domain.Entities;

/// <summary>[ParkingService] Phiên bản sơ đồ lưới của 1 tầng, lưu dạng JSON tọa độ (SRS v2 F1.4).</summary>
public class LayoutVersion : BaseEntity
{
    public int FloorId { get; set; }
    public int VersionNo { get; set; }
    /// <summary>VD: { "columns": 20, "rows": 10, "cells": [ { "x": 0, "y": 0, "type": "slot", "code": "A-01" } ] }</summary>
    public string LayoutJson { get; set; } = "{}";
    public bool IsPublished { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
    public int CreatedByUserId { get; set; }
    public string? ChangeNote { get; set; }

    public Floor Floor { get; set; } = null!;
}
