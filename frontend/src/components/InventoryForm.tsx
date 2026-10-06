import { useState } from 'react';
import type { FormEvent } from 'react';
import { api, errorMessage } from '../lib/api';
import type { Dorm, Room } from '../lib/accommodation';

type Props =
    | { kind: 'dorm'; record?: Dorm; saved: (value: Dorm) => void; cancel: () => void }
    | {
          kind: 'room';
          record?: Room;
          dormId: string;
          saved: (value: Room) => void;
          cancel: () => void;
      };

export function InventoryForm(props: Props) {
    const dorm = props.kind === 'dorm' ? props.record : undefined;
    const room = props.kind === 'room' ? props.record : undefined;
    const [name, setName] = useState(dorm?.name || room?.roomNumber || '');
    const [address, setAddress] = useState(dorm?.address || '');
    const [city, setCity] = useState(dorm?.city || '');
    const [category, setCategory] = useState(dorm?.category || '');
    const [capacity, setCapacity] = useState(String(props.record?.capacity || ''));
    const [floor, setFloor] = useState(String(room?.floor ?? 0));
    const [status, setStatus] = useState(
        props.record?.status === 'FULL'
            ? 'AVAILABLE'
            : props.record?.status || (props.kind === 'dorm' ? 'ACTIVE' : 'AVAILABLE'),
    );
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    async function submit(event: FormEvent) {
        event.preventDefault();
        if (busy) return;
        if (
            !name.trim() ||
            !Number.isInteger(Number(capacity)) ||
            Number(capacity) < 1 ||
            Number(capacity) > 2147483647 ||
            (props.kind === 'dorm' && (!address.trim() || !city.trim() || !category.trim())) ||
            (props.kind === 'room' &&
                (!floor.trim() ||
                    !Number.isInteger(Number(floor)) ||
                    Number(floor) < -2147483648 ||
                    Number(floor) > 2147483647))
        ) {
            setError('Complete all required fields and enter valid whole numbers.');
            return;
        }
        setBusy(true);
        setError('');
        try {
            if (props.kind === 'dorm') {
                props.saved(
                    await api<Dorm>('/api/dorms' + (dorm ? '/' + dorm.id : ''), {
                        method: dorm ? 'PUT' : 'POST',
                        body: JSON.stringify({
                            name: name.trim(),
                            address: address.trim(),
                            city: city.trim(),
                            category: category.trim(),
                            capacity: Number(capacity),
                            status: status === 'ACTIVE' ? 1 : 2,
                        }),
                    }),
                );
            } else {
                props.saved(
                    await api<Room>(
                        room ? '/api/rooms/' + room.id : '/api/dorms/' + props.dormId + '/rooms',
                        {
                            method: room ? 'PUT' : 'POST',
                            body: JSON.stringify({
                                roomNumber: name.trim(),
                                floor: Number(floor),
                                capacity: Number(capacity),
                                status:
                                    status === 'AVAILABLE' ? 1 : status === 'MAINTENANCE' ? 3 : 4,
                            }),
                        },
                    ),
                );
            }
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy(false);
        }
    }
    return (
        <section className="panel application-section">
            <h2>
                {props.record ? 'Edit' : 'Add'} {props.kind === 'dorm' ? 'dormitory' : 'room'}
            </h2>
            <form onSubmit={submit}>
                {error && (
                    <p className="notice error" role="alert">
                        {error}
                    </p>
                )}
                <fieldset className="competition-fields" disabled={busy}>
                    <div className="form-grid">
                        <label>
                            {props.kind === 'dorm' ? 'Dormitory name' : 'Room number'}
                            <input
                                required
                                maxLength={props.kind === 'dorm' ? 200 : 20}
                                value={name}
                                onChange={(event) => setName(event.target.value)}
                            />
                        </label>
                        {props.kind === 'dorm' ? (
                            <>
                                <label>
                                    Address
                                    <input
                                        required
                                        maxLength={250}
                                        value={address}
                                        onChange={(event) => setAddress(event.target.value)}
                                    />
                                </label>
                                <label>
                                    City
                                    <input
                                        required
                                        maxLength={100}
                                        value={city}
                                        onChange={(event) => setCity(event.target.value)}
                                    />
                                </label>
                                <label>
                                    Category
                                    <input
                                        required
                                        maxLength={100}
                                        value={category}
                                        onChange={(event) => setCategory(event.target.value)}
                                    />
                                </label>
                            </>
                        ) : (
                            <label>
                                Floor
                                <input
                                    required
                                    type="number"
                                    min="-2147483648"
                                    max="2147483647"
                                    step="1"
                                    value={floor}
                                    onChange={(event) => setFloor(event.target.value)}
                                />
                            </label>
                        )}
                        <label>
                            Capacity
                            <input
                                type="number"
                                required
                                min={Math.max(1, room?.occupiedBeds || 0)}
                                max="2147483647"
                                step="1"
                                value={capacity}
                                onChange={(event) => setCapacity(event.target.value)}
                            />
                        </label>
                        <label>
                            Status
                            <select
                                value={status}
                                onChange={(event) => setStatus(event.target.value)}
                            >
                                {(props.kind === 'dorm'
                                    ? [
                                          ['ACTIVE', 'Active'],
                                          ['INACTIVE', 'Inactive'],
                                      ]
                                    : [
                                          ['AVAILABLE', 'Available'],
                                          ['MAINTENANCE', 'Maintenance'],
                                          ['INACTIVE', 'Inactive'],
                                      ]
                                ).map(([value, label]) => (
                                    <option
                                        key={value}
                                        value={value}
                                        disabled={
                                            props.kind === 'room' &&
                                            (room?.occupiedBeds || 0) > 0 &&
                                            value !== 'AVAILABLE'
                                        }
                                    >
                                        {label}
                                    </option>
                                ))}
                            </select>
                        </label>
                    </div>
                    <p className="muted">
                        {props.kind === 'dorm'
                            ? 'Dormitory capacity must cover the combined room capacity. An occupied dormitory cannot be deactivated.'
                            : 'Full status is calculated automatically from occupancy. Occupied rooms cannot be deactivated or placed under maintenance.'}
                    </p>
                    <div className="button-row">
                        <button className="primary" disabled={busy}>
                            {busy
                                ? 'Saving…'
                                : 'Save ' + (props.kind === 'dorm' ? 'dormitory' : 'room')}
                        </button>
                        <button
                            type="button"
                            className="secondary"
                            disabled={busy}
                            onClick={props.cancel}
                        >
                            Cancel
                        </button>
                    </div>
                </fieldset>
            </form>
        </section>
    );
}
