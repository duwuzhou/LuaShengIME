type RevealValue =
  | number
  | {
      delay?: number | string
      once?: boolean
      threshold?: number
      rootMargin?: string
    }

type RevealElement = HTMLElement & {
  __revealObserver?: IntersectionObserver
}

export default defineNuxtPlugin((nuxtApp) => {
  nuxtApp.vueApp.directive('reveal', {
    mounted(el: RevealElement, binding: { value?: RevealValue }) {
      const prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
      if (prefersReducedMotion) {
        return
      }

      const rawValue = binding.value
      const options =
        typeof rawValue === 'number'
          ? { delay: rawValue }
          : rawValue ?? {}

      if (options.delay !== undefined) {
        const delay = typeof options.delay === 'number' ? `${options.delay}ms` : options.delay
        el.style.setProperty('--reveal-delay', delay)
      }

      el.classList.add('reveal-on-scroll', 'motion-before')

      const observer = new IntersectionObserver(
        ([entry]) => {
          if (entry?.isIntersecting) {
            el.classList.remove('motion-before')

            if (options.once !== false) {
              observer.unobserve(el)
            }
          } else if (options.once === false) {
            el.classList.add('motion-before')
          }
        },
        {
          threshold: options.threshold ?? 0.18,
          rootMargin: options.rootMargin ?? '0px 0px -10% 0px',
        },
      )

      observer.observe(el)
      el.__revealObserver = observer
    },

    unmounted(el: RevealElement) {
      el.__revealObserver?.disconnect()
      delete el.__revealObserver
    },
  })
})
