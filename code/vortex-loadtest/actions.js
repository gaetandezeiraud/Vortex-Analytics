import { faker } from "@faker-js/faker";

export const actions = [
  { name: "open_invoice", value: () => ({ invoice_id: `INV-${faker.number.int({ min: 1, max: 9999 })}` }) },
  { name: "create_invoice", value: () => ({ client_id: `C${faker.number.int({ min: 1, max: 9999 })}` }) },
  { name: "edit_invoice", value: () => ({ invoice_id: `INV-${faker.number.int({ min: 1, max: 9999 })}` }) },
  { name: "delete_invoice", value: () => ({ invoice_id: `INV-${faker.number.int({ min: 1, max: 9999 })}` }) },
  { name: "record_payment", value: () => ({
      invoice_id: `INV-${faker.number.int({ min: 1, max: 9999 })}`,
      amount: parseFloat(faker.finance.amount(10, 1000, 2))
  }) },
  { name: "generate_report", value: () => ({
      report_type: faker.helpers.arrayElement(["profit_loss", "balance_sheet", "cash_flow"]),
      month: `${faker.date.past({ years: 1 }).getFullYear()}-${faker.date.past({ years: 1 }).getMonth() + 1}`
  }) },
  { name: "export_csv", value: () => ({
      data_type: faker.helpers.arrayElement(["transactions", "clients", "invoices"])
  }) },
  { name: "add_client", value: () => ({ client_name: faker.company.name() }) },
  { name: "edit_client", value: () => ({ client_id: `C${faker.number.int({ min: 1, max: 9999 })}` }) },
  { name: "delete_client", value: () => ({ client_id: `C${faker.number.int({ min: 1, max: 9999 })}` }) },
  { name: "login", value: () => ({ user: faker.internet.username() }) },
  { name: "logout", value: () => ({ user: faker.internet.username() }) },
  { name: "change_settings", value: () => ({
      setting: faker.helpers.arrayElement(["currency", "tax_rate"]),
      new_value: faker.finance.currencyCode()
  }) },
  { name: "backup_data", value: () => ({}) },
  { name: "restore_data", value: () => ({}) },
];
