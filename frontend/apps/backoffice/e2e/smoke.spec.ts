import { expect, test } from '@playwright/test'

test('sin sesión, el backoffice lleva al login', async ({ page }) => {
  await page.goto('/clientes')
  await expect(page).toHaveURL(/\/ingresar\?redirect=/)
  await expect(page.getByRole('heading', { name: 'Iniciar sesión' })).toBeVisible()
})

test('el modo demo abre el panel con el menú lateral', async ({ page }) => {
  await page.goto('/ingresar')
  await page.getByRole('button', { name: 'Entrar en modo demo' }).click()
  await expect(page.getByRole('link', { name: 'Clientes' })).toBeVisible()
  await expect(page.getByRole('heading', { name: 'Dashboard' })).toBeVisible()
})
