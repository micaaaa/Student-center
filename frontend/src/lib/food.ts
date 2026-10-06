export interface Restaurant {
    id: string;
    name: string;
    address: string;
    status: number;
}
export interface Meal {
    id: string;
    type: number;
    name: string;
    description: string | null;
    price: number;
}
export interface Menu {
    id: string;
    restaurantId: string;
    date: string;
    status: number;
    meals: Meal[];
}
export const mealTypes = ['Breakfast', 'Lunch', 'Dinner'];
export interface Entitlement {
    id: string;
    studentId: string;
    academicYear: string;
    year: number;
    month: number;
    mealType: number;
    allowedQuantity: number;
    consumedQuantity: number;
    remainingQuantity: number;
    status: number;
}
export interface Purchase {
    id: string;
    mealType: number;
    quantity: number;
    unitPrice: number;
    amount: number;
    purchasedAtUtc: string;
}
export interface Consumption {
    id: string;
    restaurantId: string;
    mealType: number;
    consumedAtUtc: string;
}
export function today() {
    const date = new Date();
    return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}
