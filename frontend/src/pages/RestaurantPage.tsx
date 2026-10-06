import { useState } from 'react';
import { useParams } from 'react-router';
import { useResource } from '../hooks/useResource';
import { api, errorMessage } from '../lib/api';
import { today, mealTypes } from '../lib/food';
import type { Restaurant, Menu } from '../lib/food';
import { RequestError, StatusBadge } from '../components/ApplicationUi';
import { RestaurantForm, MenuForm } from '../components/FoodForms';
import { ConfirmationDialog } from '../components/ConfirmationDialog';

export function RestaurantPage({ management = false }: { management?: boolean }) {
    const { id } = useParams();
    const resource = useResource<Restaurant>(
        '/api/restaurants/' + (management ? 'management/' : '') + id,
    );
    if (resource.loading) return <p role="status">Loading restaurant…</p>;
    if (resource.error) return <RequestError error={resource.error} retry={resource.reload} />;
    return (
        <RestaurantRecord
            key={String(management) + id}
            item={resource.data!}
            update={resource.setData}
            management={management}
        />
    );
}

function RestaurantRecord({
    item,
    update,
    management,
}: {
    item: Restaurant;
    update: (item: Restaurant) => void;
    management: boolean;
}) {
    const [editing, setEditing] = useState(false);
    const [date, setDate] = useState(today());
    return (
        <>
            <div className="page-heading">
                <h1>{item.name}</h1>
                <p>{item.address}</p>
                <StatusBadge status={item.status === 1 ? 'ACTIVE' : 'INACTIVE'} />
            </div>
            {management && (
                <button className="secondary" disabled={editing} onClick={() => setEditing(true)}>
                    Edit restaurant
                </button>
            )}
            {editing && (
                <RestaurantForm
                    item={item}
                    cancel={() => setEditing(false)}
                    saved={(saved) => {
                        update(saved);
                        setEditing(false);
                    }}
                />
            )}
            <div className="list-toolbar application-section">
                <label>
                    Menu date
                    <input
                        type="date"
                        required
                        value={date}
                        onChange={(e) => setDate(e.target.value)}
                    />
                </label>
            </div>
            {date && (
                <DailyMenus
                    key={item.id + date}
                    restaurant={item}
                    date={date}
                    management={management}
                />
            )}
        </>
    );
}

function DailyMenus({
    restaurant,
    date,
    management,
}: {
    restaurant: Restaurant;
    date: string;
    management: boolean;
}) {
    const resource = useResource<Menu[]>(
        `/api/restaurants/${restaurant.id}/menus${management ? '/management' : ''}?from=${date}&to=${date}`,
    );
    const [editing, setEditing] = useState<Menu | 'new' | null>(null);
    const [confirmation, setConfirmation] = useState<Menu | null>(null);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');
    async function changeStatus() {
        if (!confirmation || busy) return;
        setBusy(true);
        setError('');
        setSuccess('');
        try {
            const saved = await api<Menu>(
                `/api/menus/${confirmation.id}/${confirmation.status === 1 ? 'publish' : 'withdraw'}`,
                { method: 'POST' },
            );
            resource.setData(resource.data!.map((item) => (item.id === saved.id ? saved : item)));
            setSuccess(
                saved.status === 2
                    ? 'Menu published.'
                    : 'Menu withdrawn. The draft can now be edited.',
            );
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy(false);
            setConfirmation(null);
        }
    }
    return (
        <>
            {error && (
                <div className="notice error" role="alert">
                    <p>{error}</p>
                    <button className="secondary" disabled={busy} onClick={resource.reload}>
                        Refresh menus
                    </button>
                </div>
            )}
            {success && (
                <p className="notice success" role="status">
                    {success}
                </p>
            )}
            {resource.loading ? (
                <p role="status">Loading menus…</p>
            ) : resource.error ? (
                <RequestError error={resource.error} retry={resource.reload} />
            ) : (
                <>
                    {!resource.data?.length && (
                        <section className="panel">
                            <p>
                                No {management ? '' : 'published '}menu is available for this date.
                            </p>
                            {management && (
                                <button
                                    className="primary"
                                    disabled={!!editing}
                                    onClick={() => setEditing('new')}
                                >
                                    Create menu
                                </button>
                            )}
                        </section>
                    )}
                    {resource.data?.map((menu) => (
                        <section className="panel application-section" key={menu.id}>
                            <div className="heading-row">
                                <h2>Menu · {menu.date}</h2>
                                {management && (
                                    <StatusBadge
                                        status={menu.status === 1 ? 'DRAFT' : 'PUBLISHED'}
                                    />
                                )}
                            </div>
                            {menu.meals.map((meal) => (
                                <article className="staff-document" key={meal.id}>
                                    <span className="eyebrow">{mealTypes[meal.type - 1]}</span>
                                    <h3>{meal.name}</h3>
                                    {meal.description && (
                                        <p className="preserve-lines">{meal.description}</p>
                                    )}
                                    <p>Price: {meal.price.toFixed(2)}</p>
                                </article>
                            ))}
                            {management && (
                                <>
                                    <div className="button-row">
                                        {menu.status === 1 && (
                                            <button
                                                className="secondary"
                                                disabled={busy || !!editing}
                                                onClick={() => setEditing(menu)}
                                            >
                                                Edit menu draft
                                            </button>
                                        )}
                                        <button
                                            className="primary"
                                            disabled={
                                                busy ||
                                                !!editing ||
                                                (menu.status === 1 && restaurant.status !== 1)
                                            }
                                            onClick={() => setConfirmation(menu)}
                                        >
                                            {menu.status === 1 ? 'Publish menu' : 'Withdraw menu'}
                                        </button>
                                    </div>
                                    {restaurant.status !== 1 && menu.status === 1 && (
                                        <p className="notice">
                                            Activate the restaurant before publishing this menu.
                                        </p>
                                    )}
                                </>
                            )}
                        </section>
                    ))}
                </>
            )}
            {editing && (
                <MenuForm
                    restaurantId={restaurant.id}
                    item={editing === 'new' ? undefined : editing}
                    date={date}
                    cancel={() => setEditing(null)}
                    saved={(saved) => {
                        setEditing(null);
                        resource.reload();
                        setSuccess('Menu draft saved for ' + saved.date + '.');
                    }}
                />
            )}
            {confirmation && (
                <ConfirmationDialog
                    title={confirmation.status === 1 ? 'Publish menu' : 'Withdraw menu'}
                    busy={busy}
                    onClose={() => setConfirmation(null)}
                    onConfirm={changeStatus}
                >
                    <p>
                        {confirmation.status === 1
                            ? 'Publish this menu for students to view?'
                            : 'Withdraw this menu from the student view and return it to draft status?'}
                    </p>
                    <p>
                        {restaurant.name} · {confirmation.date}
                    </p>
                </ConfirmationDialog>
            )}
        </>
    );
}
