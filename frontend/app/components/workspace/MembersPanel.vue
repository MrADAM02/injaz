<script setup lang="ts">
import type { Member, Workspace, WorkspaceRole } from '~/types/api'
import { assignableRoles, canManageMember } from '~/utils/roles'

const props = defineProps<{ workspace: Workspace; state: ReturnType<typeof useMembers> }>()

const { t } = useI18n()
const auth = useAuthStore()
const toast = useToast()
const confirm = useConfirm()
const problem = useProblem()
const format = useFormat()

const members = computed(() => props.state.members.value)
const roleOptions = computed(() => assignableRoles.map((role) => ({ value: role, label: t(`roles.${role}`) })))
const busy = ref<string | null>(null)

const manageable = (member: Member) => canManageMember(props.workspace.myRole, auth.user?.id, member)

async function changeRole(member: Member, role: WorkspaceRole) {
  if (role === member.role) return
  busy.value = member.userId
  try {
    const updated = await props.state.changeRole(member.userId, role)
    toast.success(t('members.roleChanged', { name: updated.displayName, role: t(`roles.${updated.role}`) }))
  } catch (error) {
    toast.error(error)
    await props.state.load() // put the select back to the real role
  } finally {
    busy.value = null
  }
}

async function remove(member: Member) {
  const ok = await confirm({
    title: t('members.removeConfirm', { name: member.displayName }),
    body: t('members.removeBody'),
    confirmLabel: t('common.remove'),
    danger: true,
  })
  if (!ok) return
  busy.value = member.userId
  try {
    await props.state.remove(member.userId)
    toast.success(t('members.removed', { name: member.displayName }))
  } catch (error) {
    toast.error(error)
  } finally {
    busy.value = null
  }
}
</script>

<template>
  <UiCard :title="t('settings.tabs.members')" :description="t('workspace.memberCount', members.length)">
    <div v-if="props.state.loading.value && members.length === 0" class="flex justify-center py-8 text-muted">
      <UiSpinner :label="t('common.loading')" />
    </div>
    <UiAlert v-else-if="props.state.error.value">
      {{ problem.message(props.state.error.value) }}
      <button type="button" class="ms-2 font-semibold underline" @click="props.state.load()">{{ t('common.retry') }}</button>
    </UiAlert>

    <ul v-else class="-mx-4 divide-y divide-line sm:-mx-6">
      <li v-for="member in members" :key="member.userId" class="flex flex-wrap items-center gap-3 px-4 py-3 sm:flex-nowrap sm:px-6">
        <UiAvatar :name="member.displayName" :src="member.avatarUrl" />
        <div class="min-w-0 flex-1">
          <p class="flex items-center gap-2 truncate text-sm font-medium">
            <span class="truncate">{{ member.displayName }}</span>
            <span v-if="member.userId === auth.user?.id" class="shrink-0 rounded-full bg-subtle px-2 py-0.5 text-[11px] text-muted">{{ t('common.you') }}</span>
          </p>
          <p class="truncate text-xs text-muted">
            <span dir="ltr">{{ member.email }}</span>
            <span class="hidden sm:inline"> · {{ t('members.joined', { date: format.date(member.joinedAt) }) }}</span>
          </p>
        </div>

        <div class="flex w-full items-center justify-end gap-1 sm:w-auto">
          <template v-if="manageable(member)">
            <UiSelect
              :model-value="member.role"
              :options="roleOptions"
              :label="t('members.changeRole', { name: member.displayName })"
              hide-label
              size="sm"
              :disabled="busy === member.userId"
              class="w-36"
              @update:model-value="(role) => changeRole(member, role as WorkspaceRole)"
            />
            <UiButton
              variant="danger-ghost"
              size="sm"
              icon="trash"
              :label="t('members.remove', { name: member.displayName })"
              :disabled="busy === member.userId"
              @click="remove(member)"
            />
          </template>
          <WorkspaceRoleBadge v-else :role="member.role" />
        </div>
      </li>
    </ul>
  </UiCard>
</template>
