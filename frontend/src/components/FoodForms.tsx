import { useState } from 'react';
import type { FormEvent } from 'react';
import { api, errorMessage } from '../lib/api';
import { mealTypes } from '../lib/food';
import type { Restaurant, Menu } from '../lib/food';

export function RestaurantForm({
    item,
    saved,
    cancel,
}: {
    item?: Restaurant;
    saved: (value: Restaurant) => void;
    cancel: () => void;
}) {
    const [name, setName] = useState(item?.name || '');
    const [address, setAddress] = useState(item?.address || '');
    const [status, setStatus] = useState(item?.status || 1);
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    async function submit(event: FormEvent) {
        event.preventDefault();
        if (busy || !name.trim() || !address.trim()) return;
        setBusy(true);
        setError('');
        try {
            saved(
                await api<Restaurant>('/api/restaurants' + (item ? '/' + item.id : ''), {
                    method: item ? 'PUT' : 'POST',
                    body: JSON.stringify({ name: name.trim(), address: address.trim(), status }),
                }),
            );
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy(false);
        }
    }
    return (
        <section className="panel application-section">
            <h2>{item ? 'Edit restaurant' : 'Add restaurant'}</h2>
            <form onSubmit={submit}>
                {error && (
                    <p className="notice error" role="alert">
                        {error}
                    </p>
                )}
                <fieldset className="competition-fields" disabled={busy}>
                    <div className="form-grid">
                        <label>
                            Restaurant name
                            <input
                                required
                                maxLength={200}
                                value={name}
                                onChange={(e) => setName(e.target.value)}
                            />
                        </label>
                        <label>
                            Address
                            <input
                                required
                                maxLength={250}
                                value={address}
                                onChange={(e) => setAddress(e.target.value)}
                            />
                        </label>
                        <label>
                            Status
                            <select
                                value={status}
                                onChange={(e) => setStatus(Number(e.target.value))}
                            >
                                <option value={1}>Active</option>
                                <option value={2}>Inactive</option>
                            </select>
                        </label>
                    </div>
                    <p className="muted">
                        Inactive restaurants and their menus are hidden from students.
                    </p>
                    <div className="button-row">
                        <button
                            className="primary"
                            disabled={busy || !name.trim() || !address.trim()}
                        >
                            Save restaurant
                        </button>
                        <button type="button" className="secondary" onClick={cancel}>
                            Cancel
                        </button>
                    </div>
                </fieldset>
            </form>
        </section>
    );
}

export function MenuForm({
    restaurantId,
    item,
    date,
    saved,
    cancel,
}: {
    restaurantId: string;
    item?: Menu;
    date: string;
    saved: (value: Menu) => void;
    cancel: () => void;
}) {
    const [menuDate, setMenuDate] = useState(item?.date || date);
    const [meals, setMeals] = useState(() =>
        item
            ? item.meals.map((meal) => ({
                  key: meal.id,
                  type: meal.type,
                  name: meal.name,
                  description: meal.description || '',
                  price: String(meal.price),
              }))
            : [{ key: crypto.randomUUID(), type: 1, name: '', description: '', price: '' }],
    );
    const [busy, setBusy] = useState(false);
    const [error, setError] = useState('');
    function edit(key: string, change: Partial<(typeof meals)[number]>) {
        setMeals(meals.map((meal) => (meal.key === key ? { ...meal, ...change } : meal)));
    }
    async function submit(event: FormEvent) {
        event.preventDefault();
        if (busy) return;
        if (
            !menuDate ||
            meals.some(
                (meal) =>
                    !meal.name.trim() ||
                    !/^\d+(\.\d{1,2})?$/.test(meal.price) ||
                    Number(meal.price) > 99999999.99,
            )
        ) {
            setError('Enter meal names and non-negative prices with at most two decimal places.');
            return;
        }
        setBusy(true);
        setError('');
        try {
            saved(
                await api<Menu>(
                    item ? '/api/menus/' + item.id : `/api/restaurants/${restaurantId}/menus`,
                    {
                        method: item ? 'PUT' : 'POST',
                        body: JSON.stringify({
                            date: menuDate,
                            meals: meals.map((meal) => ({
                                type: meal.type,
                                name: meal.name.trim(),
                                description: meal.description.trim() || null,
                                price: Number(meal.price),
                            })),
                        }),
                    },
                ),
            );
        } catch (error) {
            setError(errorMessage(error));
        } finally {
            setBusy(false);
        }
    }
    return (
        <section className="panel application-section">
            <h2>{item ? 'Edit menu draft' : 'Create menu draft'}</h2>
            <form onSubmit={submit}>
                {error && (
                    <p className="notice error" role="alert">
                        {error}
                    </p>
                )}
                <fieldset className="competition-fields" disabled={busy}>
                    <label>
                        Menu date
                        <input
                            type="date"
                            required
                            value={menuDate}
                            onChange={(e) => setMenuDate(e.target.value)}
                        />
                    </label>
                    {meals.map((meal, index) => (
                        <fieldset className="staff-document competition-fields" key={meal.key}>
                            <legend>Meal {index + 1}</legend>
                            <div className="form-grid">
                                <label>
                                    Meal type
                                    <select
                                        value={meal.type}
                                        onChange={(e) =>
                                            edit(meal.key, { type: Number(e.target.value) })
                                        }
                                    >
                                        {mealTypes.map((name, i) => (
                                            <option key={name} value={i + 1}>
                                                {name}
                                            </option>
                                        ))}
                                    </select>
                                </label>
                                <label>
                                    Meal name
                                    <input
                                        required
                                        maxLength={200}
                                        value={meal.name}
                                        onChange={(e) => edit(meal.key, { name: e.target.value })}
                                    />
                                </label>
                                <label>
                                    Price
                                    <input
                                        required
                                        type="number"
                                        min="0"
                                        max="99999999.99"
                                        step="0.01"
                                        value={meal.price}
                                        onChange={(e) => edit(meal.key, { price: e.target.value })}
                                    />
                                </label>
                            </div>
                            <label>
                                Description
                                <textarea
                                    rows={2}
                                    maxLength={2000}
                                    value={meal.description}
                                    onChange={(e) =>
                                        edit(meal.key, { description: e.target.value })
                                    }
                                />
                            </label>
                            <button
                                type="button"
                                className="secondary"
                                disabled={meals.length === 1}
                                onClick={() =>
                                    setMeals(meals.filter((value) => value.key !== meal.key))
                                }
                            >
                                Remove meal {index + 1}
                            </button>
                        </fieldset>
                    ))}
                    <div className="button-row">
                        <button
                            type="button"
                            className="secondary"
                            disabled={meals.length >= 30}
                            onClick={() =>
                                setMeals([
                                    ...meals,
                                    {
                                        key: crypto.randomUUID(),
                                        type: 1,
                                        name: '',
                                        description: '',
                                        price: '',
                                    },
                                ])
                            }
                        >
                            Add meal
                        </button>
                        <button className="primary">Save menu draft</button>
                        <button type="button" className="secondary" onClick={cancel}>
                            Cancel
                        </button>
                    </div>
                    <p className="muted">
                        A daily menu contains 1–30 meals. Saving a draft does not publish it.
                    </p>
                </fieldset>
            </form>
        </section>
    );
}
