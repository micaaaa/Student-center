export interface Charge {
    id: string;
    studentId: string;
    type: 'Accommodation' | 'Meal' | 'Card' | 'Other';
    amount: number;
    currency: string;
    dueDate: string;
    status: string;
    referenceId: string;
    period: string | null;
    description: string;
    createdAtUtc: string;
    paidAmount: number;
    outstandingAmount: number;
    paidAtUtc: string | null;
}
export interface Payment {
    id: string;
    chargeId: string;
    studentId: string;
    amount: number;
    currency: string;
    method: 'Cash' | 'Card' | 'BankTransfer';
    referenceNumber: string | null;
    paymentDateUtc: string;
}
export interface Balance {
    currency: string;
    chargeCount: number;
    outstandingAmount: number;
    overdueAmount: number;
    totalChargedAmount: number;
    totalPaidAmount: number;
}
export const chargeTypes: Record<number, string> = { 1: 'Accommodation', 3: 'Card', 4: 'Other' };
export const chargeTypeLabels: Record<Charge['type'], string> = {
    Accommodation: 'Accommodation',
    Meal: 'Meals',
    Card: 'Card',
    Other: 'Other',
};
export const paymentMethodValues = ['Cash', 'Card', 'BankTransfer'] as const;
export const paymentMethodLabels: Record<Payment['method'], string> = {
    Cash: 'Cash',
    Card: 'Card',
    BankTransfer: 'Bank transfer',
};
export const paymentMethods = ['Cash', 'Card', 'Bank transfer'];
export const money = (amount: number) =>
    new Intl.NumberFormat('en-GB', { style: 'currency', currency: 'RSD' }).format(amount);
export function pendingBillingKey(studentId: string, chargeId?: string) {
    return `${studentId}.${chargeId || 'new-charge'}`;
}
