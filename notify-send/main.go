package main

import (
	"fmt"
	"os"
)

func main() {
	title := os.Args[1]
	body := os.Args[2]

	message := fmt.Sprintf(`{ "title": %q, "content": %q }`, title, body)

	// send a message to a named pipe BetterNotifications
	pipeName := "\\\\.\\pipe\\BetterNotifications"
	pipe, err := os.OpenFile(pipeName, os.O_WRONLY, 0)
	if err != nil {
		fmt.Println("Error opening pipe:", err)
		return
	}
	defer pipe.Close()

	pipe.WriteString(message)
	pipe.Sync()
}
