// Zvonjenje: početak i kraj svakog časa (veliki odmor posle 2. časa).
export const lessonTimes = [
  ['08:00', '08:45'], ['08:50', '09:35'], ['09:55', '10:40'], ['10:45', '11:30'],
  ['11:35', '12:20'], ['12:25', '13:10'], ['13:15', '14:00'],
] as const

export const dayNames = ['Ponedeljak', 'Utorak', 'Sreda', 'Četvrtak', 'Petak']

const minutes = (time: string) => Number(time.slice(0, 2)) * 60 + Number(time.slice(3))

// Današnji radni dan (1–5) ili null vikendom.
export function schoolDay(now = new Date()) {
  const day = now.getDay()
  return day >= 1 && day <= 5 ? day : null
}

// Redni broj časa koji je upravo u toku, ili null (pre nastave, na odmoru, posle nastave).
export function currentLesson(now = new Date()) {
  const current = now.getHours() * 60 + now.getMinutes()
  const index = lessonTimes.findIndex(([start, end]) => current >= minutes(start) && current < minutes(end))
  return index === -1 ? null : index + 1
}

// Prvi čas koji još nije počeo danas.
export function nextLessonNumber(now = new Date()) {
  const current = now.getHours() * 60 + now.getMinutes()
  const index = lessonTimes.findIndex(([start]) => minutes(start) > current)
  return index === -1 ? null : index + 1
}

export const lessonTime = (lessonNumber: number) => lessonTimes[lessonNumber - 1]?.join('–') ?? ''
