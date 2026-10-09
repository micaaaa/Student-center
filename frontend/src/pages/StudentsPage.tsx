import { StudentAccommodationRecords } from '../components/StudentAccommodationRecords';
import { Link, useParams } from 'react-router';
import { useAuth } from '../auth/AuthContext';
import { StudentSearch } from '../components/StudentSearch';
import { RequestError, StatusBadge } from '../components/ApplicationUi';
import { useResource } from '../hooks/useResource';
import type { Student } from '../lib/types';

export function StudentsPage() {
    return (
        <>
            <div className="page-heading">
                <span className="eyebrow">STUDENT RECORDS</span>
                <h1>Users</h1>
                <p className="muted">
                    Find a student by name or student number and open their record. Only students
                    who have created a profile appear here.
                </p>
            </div>
            <StudentSearch />
        </>
    );
}

export function StudentPage() {
    const { id } = useParams();
    const { user } = useAuth();
    const resource = useResource<Student>('/api/students/' + id);
    const student = resource.data;
    return (
        <>
            {resource.loading ? (
                <p role="status">Loading student…</p>
            ) : resource.error ? (
                <RequestError error={resource.error} retry={resource.reload} />
            ) : (
                student && (
                    <>
                        <div className="page-heading">
                            <span className="eyebrow">STUDENT RECORD</span>
                            <h1>
                                {student.firstName} {student.lastName}
                            </h1>
                            <p>{student.studentNumber}</p>
                            <StatusBadge status={student.status} />
                        </div>
                        <section className="panel">
                            <h2>Student information</h2>
                            <dl className="details-grid">
                                <div>
                                    <dt>Email</dt>
                                    <dd>{student.email}</dd>
                                </div>
                                <div>
                                    <dt>Phone</dt>
                                    <dd>{student.phone || 'Not provided'}</dd>
                                </div>
                                <div>
                                    <dt>Faculty</dt>
                                    <dd>{student.faculty || 'Not provided'}</dd>
                                </div>
                                <div>
                                    <dt>Study program</dt>
                                    <dd>{student.studyProgram || 'Not provided'}</dd>
                                </div>
                                <div>
                                    <dt>Year of study</dt>
                                    <dd>{student.yearOfStudy || 'Not provided'}</dd>
                                </div>
                            </dl>
                        </section>
                        {user?.permissions.includes('ManageAccommodation') && (
                            <StudentAccommodationRecords studentId={student.id} />
                        )}
                        <div className="button-row application-section">
                            {user?.permissions.includes('ManageApplications') && (
                                <Link
                                    className="primary"
                                    to={'/staff/applications?studentId=' + student.id}
                                >
                                    Applications
                                </Link>
                            )}
                            {user?.permissions.includes('ManageMaintenance') && (
                                <Link
                                    className="primary"
                                    to={'/staff/maintenance?studentId=' + student.id}
                                >
                                    Repair requests
                                </Link>
                            )}
                            {user?.permissions.includes('ManageFood') && (
                                <Link
                                    className="primary"
                                    to={'/staff/meals?studentId=' + student.id}
                                >
                                    Meals
                                </Link>
                            )}
                            {user?.permissions.includes('ManageBilling') && (
                                <Link
                                    className="primary"
                                    to={'/staff/billing?studentId=' + student.id}
                                >
                                    Charges and payments
                                </Link>
                            )}
                        </div>
                    </>
                )
            )}
        </>
    );
}
