const express = require('express')
const cors = require('cors')
const fromEntries = require('fromentries')

const app = express()
app.use(express.json())
app.use(cors())

let data = {}
const idMap = new Map()
const muted = new Set()

app.post('/', (req, res) => {
    data = req.body
//  console.log(data)
    res.send('Proximity data posted.')
})

app.get('/map', (req, res) => {
    res.send(fromEntries(idMap))
})

app.get('/raw', (req, res) => {
    res.send(data)
})

app.post('/muted', (req, res) => {
    const name = req.body.name
    if (req.body.mute) {
	muted.add(name)
    } else {
	muted.delete(name)
    }
    res.send('Mute change successful.')
})

app.get('/muted', (req, res) => {
    res.send(Array.from(muted))
})

app.get('/:name', (req, res) => {
    const name = req.params.name
    if (!data.hasOwnProperty(name)) {
        res.status(404).send('Player not found.')
    } else {
        res.send(data[name])
    }
})

app.post('/:name', (req, res) => {
    const name = req.params.name
    idMap.set(name, req.body.Id)
    res.send('Player ID mapped.')
})

app.delete('/:name', (req, res) => {
    const name = req.params.name
    idMap.delete(name)
    res.send('Player ID unmapped.')
})

let port = process.env.PORT
if (port == null || port == "") {
    port = 2021;
}
app.listen(port, () => {
    console.log(`API listening at http://localhost:${port}`)
})
