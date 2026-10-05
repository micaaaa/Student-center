import { Link } from 'react-router';
import { errorMessage } from '../lib/api';
import { statusLabel } from '../lib/applications';

export function RequestError({ error, retry }: { error: unknown; retry: () => void }) {
    const message = errorMessage(error);
    return (
        <div className="notice error" role="alert">
            <p>{message}</p>
            <div className="button-row">
                <button type="button" className="secondary" onClick={retry}>
                    Try again
                </button>
                {message === 'Create a student profile before managing applications.' && (
                    <Link className="primary" to="/profile">
                        Create student profile
                    </Link>
                )}
            </div>
        </div>
    );
}

export function StatusBadge({ status }: { status: string }) {
    return (
        <span className={'status-badge status-' + status.toLowerCase().replaceAll('_', '-')}>
            {statusLabel(status)}
        </span>
    );
}
