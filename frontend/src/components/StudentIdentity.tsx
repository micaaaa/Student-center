import { Link } from 'react-router';
import { useResource } from '../hooks/useResource';
import type { Student } from '../lib/types';

export function StudentIdentity({ id }: { id: string }) {
    const student = useResource<Student>('/api/students/' + id + '/summary');
    if (student.loading) return <span>Loading student…</span>;
    if (student.error)
        return (
            <span>
                Student details unavailable.{' '}
                <button type="button" className="text-link" onClick={student.reload}>
                    Retry
                </button>
            </span>
        );
    return (
        <Link className="text-link" to={'/staff/students/' + id}>
            {student.data!.firstName} {student.data!.lastName} · {student.data!.studentNumber}
        </Link>
    );
}

export function StudentOption({ id, value, year }: { id: string; value: string; year: string }) {
    const student = useResource<Student>('/api/students/' + id + '/summary');
    return (
        <option value={value} disabled={!student.data}>
            {student.data
                ? `${student.data.firstName} ${student.data.lastName} · ${student.data.studentNumber} — ${year}`
                : student.loading
                  ? 'Loading student…'
                  : 'Student unavailable — refresh the page'}
        </option>
    );
}
