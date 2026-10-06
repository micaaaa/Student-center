export interface Dorm {
    id: string;
    name: string;
    address: string;
    city: string;
    category: string;
    capacity: number;
    status: string;
}

export interface Room {
    id: string;
    dormId: string;
    roomNumber: string;
    floor: number;
    capacity: number;
    occupiedBeds: number;
    status: string;
}

export interface ReceivedEligibility {
    id: string;
    studentId: string;
    competitionId: string;
    academicYear: string;
    grantedAtUtc: string;
}

export interface Assignment {
    id: string;
    eligibilityId: string;
    studentId: string;
    roomId: string;
    academicYear: string;
    status: string;
    assignedAtUtc: string;
    cancelledAtUtc: string | null;
    cancellationReason: string | null;
    moveIn: { dateUtc: string; medicalCertificateReference: string } | null;
    moveOut: { dateUtc: string; reason: string } | null;
}

export interface MyAccommodation {
    id: string;
    academicYear: string;
    status: string;
    assignedAtUtc: string;
    movedInAtUtc: string | null;
    movedOutAtUtc: string | null;
    cancelledAtUtc: string | null;
    dorm: { id: string; name: string; address: string; city: string };
    room: { id: string; number: string; floor: number };
}
