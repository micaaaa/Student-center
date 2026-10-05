import { useEffect, useState } from 'react';

export interface Competition {
    id: string;
    academicYear: string;
    name: string;
    description: string | null;
    applicationStartDateUtc: string;
    applicationEndDateUtc: string;
    status: string;
}

export interface StudentApplication {
    id: string;
    competitionId: string;
    studentId: string;
    status: string;
    createdAtUtc: string;
    submittedAtUtc: string | null;
    note: string | null;
}

export interface ApplicationDocument {
    id: string;
    applicationId: string;
    documentType: string;
    fileName: string;
    contentType: string;
    size: number;
    status: string;
    uploadedAtUtc: string;
    reviewedAtUtc: string | null;
    reviewComment: string | null;
}

export const documentTypes = [
    { value: 1, name: 'EnrollmentConfirmation', label: 'Enrollment confirmation' },
    { value: 2, name: 'IncomeCertificate', label: 'Income certificate' },
    { value: 3, name: 'Transcript', label: 'Academic transcript' },
    { value: 4, name: 'IdentityDocument', label: 'Identity document' },
    { value: 5, name: 'Other', label: 'Other supporting document' },
];

export function utcDate(value: string) {
    // SQL-backed UTC fields may be serialized without an explicit timezone suffix.
    return new Date(/[zZ]$|[+-]\d{2}:\d{2}$/.test(value) ? value : value + 'Z');
}

export function dateTime(value: string | null) {
    if (!value) return 'Not submitted';
    return new Intl.DateTimeFormat('en-GB', {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        timeZone: 'Europe/Belgrade',
        timeZoneName: 'short',
    }).format(utcDate(value));
}

export function acceptsApplications(competition: Competition, now: number) {
    return (
        competition.status === 'OPEN' &&
        now >= utcDate(competition.applicationStartDateUtc).getTime() &&
        now <= utcDate(competition.applicationEndDateUtc).getTime()
    );
}

export function useNow() {
    const [now, setNow] = useState(Date.now());
    useEffect(() => {
        const timer = window.setInterval(() => setNow(Date.now()), 1000);
        return () => window.clearInterval(timer);
    }, []);
    return now;
}

export function statusLabel(status: string) {
    return status
        .toLowerCase()
        .replaceAll('_', ' ')
        .replace(/^./, (value) => value.toUpperCase());
}
