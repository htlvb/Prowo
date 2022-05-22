<style>
* {
	font-family: "Helvetica";
}
h1 {
	font-variant: small-caps;
}
table {
	border: 1px solid black;
	width: 100%;
}
td {
	border: 1px solid grey;
}
td:last-child {
	width: 100%;
}
.date {
	font-size: 80%;
	color: grey
}
</style>

# Teilnehmerliste "{{ project.title }}"
<span class="date">{{ project.date }}</span>

| Klasse | Name | Notizen |
| ------ | ---- | ------- |
{% for attendee in project.attendees -%}
| {attendee.lastName} {attendee.firstName} | {attendee.class} | |
{% endfor -%}
