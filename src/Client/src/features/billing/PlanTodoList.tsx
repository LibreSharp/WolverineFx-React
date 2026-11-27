import { useEffect } from 'react'
import { Link } from 'react-router'
import { useQueryClient } from '@tanstack/react-query'
import { Alert, AlertDescription } from '@/components/ui/alert'
import { TodoList } from '../todos/TodoList'
import { useTodos } from '../todos/todoQueries'
import { billingKeys, useBilling } from './billingQueries'

// The Todo list within the organisation's plan: reminders only if the plan includes them, and a
// warning as open Todos near the plan's limit. The API enforces both (402 Payment Required); this
// only explains them before the user runs into them.
export function PlanTodoList() {
  const billing = useBilling()
  const todos = useTodos()
  const queryClient = useQueryClient()

  // Open Todos count towards the limit, so refresh the usage whenever the list changes (here or,
  // through realtime updates, in another tab).
  const refreshBilling = () => void queryClient.invalidateQueries({ queryKey: billingKeys.summary })
  useEffect(refreshBilling, [todos.dataUpdatedAt, queryClient])

  const plan = billing.data
  const open = plan?.limits.find((limit) => limit.name === 'OpenTodos')
  const notice =
    plan && open && (open.reached || open.nearLimit) ? (
      <Alert role="status" className="mb-4 border-amber-200 bg-amber-50 text-amber-900">
        <AlertDescription className="text-amber-900">
        {open.reached
          ? `You've reached the ${plan.planName} plan's ${open.limit} open Todos: complete some, or `
          : `${open.used} of the ${plan.planName} plan's ${open.limit} open Todos used. Need more? `}
        <Link to="/pricing" className="font-medium underline">
          upgrade
        </Link>
        .
        </AlertDescription>
      </Alert>
    ) : undefined

  return (
    <TodoList
      // Until the plan has loaded, offer reminders; the API refuses them if the plan doesn't include them.
      remindersEnabled={plan ? plan.features.includes('TodoReminders') : true}
      reminderUpsell={
        <Link to="/pricing" className="text-sm text-muted-foreground hover:underline">
          Reminders: upgrade
        </Link>
      }
      notice={notice}
      onReminderSet={refreshBilling}
    />
  )
}
